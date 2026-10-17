using System.Globalization;
using Maydel.Application.Abstractions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Maydel.Infrastructure.Reports;

/// <summary>The admission letter (E11.8): A4 on the school letterhead, signed with the principal's uploaded signature.</summary>
internal sealed class QuestPdfAdmissionLetterRenderer : IAdmissionLetterRenderer
{
    private static readonly Lazy<byte[]> DefaultLogo = new(() =>
    {
        using var stream = typeof(QuestPdfAdmissionLetterRenderer).Assembly.GetManifestResourceStream("Maydel.ReportLogo.png")
            ?? throw new InvalidOperationException("Report logo resource missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    });

    private const string Navy = "#234a6d";

    static QuestPdfAdmissionLetterRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Render(AdmissionLetterDocument l, ReportBranding school)
    {
        static string D(DateOnly d) => d.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginHorizontal(50);
            page.MarginVertical(40);
            page.DefaultTextStyle(t => t.FontSize(11).FontColor("#1d2733").LineHeight(1.4f));
            page.Header().BorderBottom(2).BorderColor(Navy).PaddingBottom(10).Row(row =>
            {
                row.ConstantItem(64).Height(64).Image(school.Logo ?? DefaultLogo.Value).FitArea();
                row.RelativeItem().PaddingLeft(12).Column(c =>
                {
                    c.Item().Text(school.Name.ToUpperInvariant()).FontSize(16).Bold().FontColor(Navy);
                    if (school.Motto is { } motto) c.Item().Text(motto).FontSize(8).Italic();
                    c.Item().Text(school.Address).FontSize(8);
                    var contact = string.Join(" · ", new[] { school.Phone, school.Email, school.Website }.Where(x => !string.IsNullOrWhiteSpace(x)));
                    if (contact.Length > 0) c.Item().Text(contact).FontSize(8);
                });
            });
            page.Content().PaddingTop(20).Column(col =>
            {
                col.Spacing(10);
                col.Item().Row(r =>
                {
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text(l.ParentName);
                        if (l.HomeAddress is { } address) c.Item().Text(address).FontSize(10);
                    });
                    r.ConstantItem(170).AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text(D(l.LetterDate));
                        c.Item().AlignRight().Text($"Ref: {l.ReferenceNumber}").FontSize(9);
                    });
                });
                col.Item().PaddingTop(6).Text("Dear Parent/Guardian,");
                col.Item().Text($"OFFER OF ADMISSION: {l.ChildName.ToUpperInvariant()}").Bold().FontColor(Navy);
                col.Item().Text(t =>
                {
                    t.Span("We are pleased to inform you that, following the entrance examination, ");
                    t.Span(l.ChildName).Bold();
                    t.Span(" has been offered admission into ");
                    t.Span(l.ClassLevel).Bold();
                    t.Span($" for the {l.Session} academic session.");
                });
                col.Item().Background("#ebf1f7").Padding(10).Row(r =>
                {
                    r.RelativeItem().Text(t => { t.Span("Admission number: ").FontSize(10); t.Span(l.AdmissionNumber).Bold(); });
                    if (l.ResumesOn is { } resumes)
                        r.RelativeItem().AlignRight().Text(t => { t.Span("School resumes: ").FontSize(10); t.Span(D(resumes)).Bold(); });
                });
                col.Item().Text("To take up this offer, please accept it in your admissions account. Once enrolment is complete, the same login opens the " +
                    "parent portal, where you can view and pay school fees, follow your child's results and receive school notices.");
                col.Item().Text("The first invoice includes the new-intake items (such as uniforms and books). Please keep this letter for your records.");
                col.Item().Text("We look forward to welcoming you to our school family.");
                col.Item().PaddingTop(10).Text("Yours faithfully,");
                col.Item().Height(50).Width(160).Element(e =>
                {
                    if (school.Signature is { Length: > 0 } signature) e.Image(signature).FitArea();
                });
                col.Item().Width(200).BorderTop(0.75f).BorderColor(Colors.Grey.Darken1).PaddingTop(3).Column(c =>
                {
                    c.Item().Text(school.PrincipalName ?? "The Principal").Bold();
                    c.Item().Text("Principal").FontSize(9);
                });
            });
            page.Footer().AlignCenter().Text($"{school.Name} · Admission letter {l.AdmissionNumber}").FontSize(7).FontColor(Colors.Grey.Darken1);
        }))
        .WithMetadata(new DocumentMetadata { Title = $"Admission letter {l.AdmissionNumber}", Author = school.Name, CreationDate = l.LetterDate.ToDateTime(TimeOnly.MinValue), ModifiedDate = l.LetterDate.ToDateTime(TimeOnly.MinValue) })
        .GeneratePdf();
    }
}
