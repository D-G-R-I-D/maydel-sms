using System.Globalization;
using Maydel.Application.Abstractions;
using Maydel.Domain.Admissions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Maydel.Infrastructure.Reports;

/// <summary>The admission form (E11.5): blank with writing lines, or filled from an application. A4, school letterhead.</summary>
internal sealed class QuestPdfAdmissionFormRenderer : IAdmissionFormRenderer
{
    private static readonly Lazy<byte[]> DefaultLogo = new(() =>
    {
        using var stream = typeof(QuestPdfAdmissionFormRenderer).Assembly.GetManifestResourceStream("Maydel.ReportLogo.png")
            ?? throw new InvalidOperationException("Report logo resource missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    });

    private const string Navy = "#234a6d";
    private const string Fill = "#ebf1f7";

    static QuestPdfAdmissionFormRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Render(AdmissionFormDocument document, ReportBranding school)
    {
        var a = document.Application;
        var f = a?.Form ?? new ApplicationForm();
        var app = a?.Application;
        string V(string? value) => a is null ? "" : value ?? "—";

        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(30);
            page.DefaultTextStyle(t => t.FontSize(9).FontColor("#1d2733"));
            page.Header().BorderBottom(2).BorderColor(Navy).PaddingBottom(8).Row(row =>
            {
                row.ConstantItem(54).Height(54).Image(school.Logo ?? DefaultLogo.Value).FitArea();
                row.RelativeItem().PaddingHorizontal(8).Column(c =>
                {
                    c.Item().Text(school.Name.ToUpperInvariant()).FontSize(14).Bold().FontColor(Navy);
                    c.Item().Text(school.Address).FontSize(7);
                    c.Item().PaddingTop(4).Text($"APPLICATION FOR ADMISSION · {document.Session} SESSION").FontSize(11).Bold();
                    if (app is not null) c.Item().Text($"Reference {app.ReferenceNumber} · Applying for {app.ClassLevel}").FontSize(8);
                });
                row.ConstantItem(80).Height(96).Border(0.5f).BorderColor(Colors.Grey.Medium).Element(e =>
                {
                    if (document.Photo is { Length: > 0 } photo) e.Image(photo).FitArea();
                    else e.AlignCenter().AlignMiddle().Text("Affix passport\nphotograph").FontSize(7).FontColor(Colors.Grey.Darken1);
                });
            });
            page.Content().PaddingTop(10).Column(col =>
            {
                col.Spacing(8);
                Section(col, "CHILD", [
                    ("Surname", V(app?.ChildLastName)), ("First name", V(app?.ChildFirstName)), ("Middle name", V(f.ChildMiddleName)),
                    ("Date of birth", a is null ? "" : app?.ChildDateOfBirth?.ToString("d MMMM yyyy", CultureInfo.InvariantCulture) ?? "—"),
                    ("Sex", a is null ? "" : f.ChildSex?.ToString() ?? "—"), ("State of origin", V(f.StateOfOrigin)), ("LGA", V(f.Lga)), ("Religion", V(f.Religion)),
                    ("Home address", V(f.HomeAddress)), ("Previous school", V(f.PreviousSchool)), ("Previous class", V(f.PreviousClass)),
                    ("Medical notes / allergies", V(f.MedicalNotes)), ("Special needs", V(f.SpecialNeeds)),
                ]);
                Section(col, "FATHER / GUARDIAN", [("Name", V(f.FatherName)), ("Phone", V(f.FatherPhone)), ("Email", V(f.FatherEmail)), ("Occupation", V(f.FatherOccupation))]);
                Section(col, "MOTHER / GUARDIAN", [("Name", V(f.MotherName)), ("Phone", V(f.MotherPhone)), ("Email", V(f.MotherEmail)), ("Occupation", V(f.MotherOccupation))]);
                Section(col, "EMERGENCY CONTACT", [("Name", V(f.EmergencyContactName)), ("Phone", V(f.EmergencyContactPhone))]);
                col.Item().Background(Fill).Padding(8).Column(d =>
                {
                    d.Item().Text("DECLARATION").FontSize(8).Bold().FontColor(Navy);
                    d.Item().PaddingTop(2).Text("I declare that the information given on this form is true and complete, and I agree to abide by the rules and regulations of the school.");
                    d.Item().PaddingTop(10).Row(r =>
                    {
                        r.RelativeItem().Text(a is null ? "Signature: ______________________________" : f.Declaration ? $"Confirmed online by {app!.ParentName}" : "Not yet confirmed");
                        r.RelativeItem().AlignRight().Text(a?.SubmittedAt is { } at ? $"Submitted {at:d MMMM yyyy}" : "Date: ____________________");
                    });
                });
            });
            page.Footer().Row(r =>
            {
                r.RelativeItem().Text(a is null ? "Return the completed form with a passport photograph and a copy of the birth certificate." : $"Printed {document.PrintedOn:d MMM yyyy}")
                    .FontSize(7).FontColor(Colors.Grey.Darken1);
                r.ConstantItem(140).AlignRight().Text(school.Name).FontSize(7).FontColor(Colors.Grey.Darken1);
            });
        }))
        .WithMetadata(new DocumentMetadata { Title = app is null ? "Admission form" : $"Admission form {app.ReferenceNumber}", Author = school.Name })
        .GeneratePdf();
    }

    private static void Section(ColumnDescriptor col, string title, (string Label, string Value)[] rows) =>
        col.Item().Column(s =>
        {
            s.Item().Background(Navy).PaddingVertical(3).PaddingHorizontal(6).Text(title).FontSize(8).Bold().FontColor(Colors.White);
            s.Item().Table(t =>
            {
                t.ColumnsDefinition(c => { c.ConstantColumn(150); c.RelativeColumn(); });
                foreach (var (label, value) in rows)
                {
                    t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingVertical(5).PaddingHorizontal(6).Text(label).FontSize(8).FontColor(Colors.Grey.Darken2);
                    t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingVertical(5).PaddingHorizontal(6).Text(value);
                }
            });
        });
}
