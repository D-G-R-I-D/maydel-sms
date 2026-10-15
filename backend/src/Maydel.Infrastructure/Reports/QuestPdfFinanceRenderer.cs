using System.Globalization;
using Maydel.Application.Abstractions;
using Maydel.Domain.Finance;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Maydel.Infrastructure.Reports;

/// <summary>
/// Invoices (A4) and receipts (A5) with the school's letterhead. The student's admission number is printed large on
/// both (it's the reference parents quote when paying). Amounts are written "NGN" so every PDF font can show them.
/// </summary>
internal sealed class QuestPdfFinanceRenderer : IFinanceDocumentRenderer
{
    private static readonly Lazy<byte[]> DefaultLogo = new(() =>
    {
        using var stream = typeof(QuestPdfFinanceRenderer).Assembly.GetManifestResourceStream("Maydel.ReportLogo.png")
            ?? throw new InvalidOperationException("Report logo resource missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    });

    private const string Navy = "#234a6d";
    private const string Gold = "#b8924a";
    private const string Fill = "#ebf1f7";
    private static readonly CultureInfo Money = CultureInfo.InvariantCulture;

    static QuestPdfFinanceRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Invoice(InvoiceDocument document, ReportBranding school)
    {
        var i = document.Invoice;
        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(32);
            page.DefaultTextStyle(t => t.FontSize(9).FontColor("#1d2733"));
            var stamp = i.Status switch { InvoiceStatus.Paid => "PAID", InvoiceStatus.Cancelled => "CANCELLED", _ => null };
            if (stamp is not null)
                page.Background().AlignCenter().AlignMiddle().Rotate(-30).Text(stamp).FontSize(90).Bold().FontColor("#e6edf3");

            page.Header().Element(h => Letterhead(h, school, "INVOICE", [("Invoice no.", i.Number), ("Issued", i.IssuedAt.ToString("d MMM yyyy", Money)),
                ("Due", i.DueOn?.ToString("d MMM yyyy", Money) ?? "—"), ("Status", Status(i.Status))]));
            page.Content().PaddingTop(12).Column(col =>
            {
                col.Spacing(12);
                col.Item().Element(c => StudentBox(c, i.StudentName, i.AdmissionNumber, i.ClassRoom, $"{i.Term} {i.Session}", document.Guardian));
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(120); });
                    t.Header(h => { HeadCell(h.Cell(), "DESCRIPTION"); HeadCell(h.Cell().AlignRight(), "AMOUNT"); });
                    foreach (var line in i.Lines)
                    {
                        Cell(t.Cell()).Text(line.Description);
                        Cell(t.Cell()).AlignRight().Text(Ngn(line.Amount)).FontColor(line.Amount < 0 ? "#1f7a3d" : "#1d2733");
                    }
                });
                col.Item().AlignRight().Width(240).Column(totals =>
                {
                    Total(totals, "Total", i.Total, bold: false);
                    Total(totals, "Paid", i.Paid, bold: false);
                    Total(totals, "Balance", i.Balance, bold: true);
                    if (i.Overdue > 0) Total(totals, "Overdue now", i.Overdue, bold: true, colour: "#b42332");
                });
                if (i.Schedule.Count > 0)
                    col.Item().Column(s =>
                    {
                        s.Item().Text("INSTALMENTS").FontSize(8).Bold().FontColor(Navy);
                        s.Item().PaddingTop(4).Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.ConstantColumn(70); c.ConstantColumn(60); c.RelativeColumn(); c.ConstantColumn(120); });
                            t.Header(h => { HeadCell(h.Cell(), "PART"); HeadCell(h.Cell(), "SHARE"); HeadCell(h.Cell(), "DUE BY"); HeadCell(h.Cell().AlignRight(), "AMOUNT"); });
                            foreach (var part in i.Schedule)
                            {
                                Cell(t.Cell()).Text($"{part.Order} of {i.Schedule.Count}");
                                Cell(t.Cell()).Text($"{part.Percent:0.##}%");
                                Cell(t.Cell()).Text(part.DueOn.ToString("dddd, d MMMM yyyy", Money));
                                Cell(t.Cell()).AlignRight().Text(Ngn(part.Amount));
                            }
                        });
                    });
                if (i.Payments.Count > 0)
                    col.Item().Column(p =>
                    {
                        p.Item().Text("PAYMENTS").FontSize(8).Bold().FontColor(Navy);
                        p.Item().PaddingTop(4).Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.ConstantColumn(80); c.ConstantColumn(130); c.ConstantColumn(80); c.RelativeColumn(); c.ConstantColumn(110); });
                            t.Header(h => { HeadCell(h.Cell(), "DATE"); HeadCell(h.Cell(), "RECEIPT"); HeadCell(h.Cell(), "METHOD"); HeadCell(h.Cell(), "REFERENCE"); HeadCell(h.Cell().AlignRight(), "AMOUNT"); });
                            foreach (var pay in i.Payments)
                            {
                                Cell(t.Cell()).Text(pay.PaidOn.ToString("d MMM yyyy", Money));
                                Cell(t.Cell()).Text(pay.ReceiptNumber);
                                Cell(t.Cell()).Text(pay.IsReversal ? "Reversal" : Method(pay.Method));
                                Cell(t.Cell()).Text(pay.IsReversal ? $"Reverses {pay.ReversesReceipt}" : pay.Reference ?? "");
                                Cell(t.Cell()).AlignRight().Text(Ngn(pay.Amount));
                            }
                        });
                    });
                if (i.CancelReason is { } reason) col.Item().Text($"Cancelled: {reason}").Italic().FontColor("#b42332");
                col.Item().Background(Fill).Padding(8).Text(t =>
                {
                    t.Span("How to pay: ").Bold();
                    t.Span("quote the admission number ");
                    t.Span(i.AdmissionNumber).Bold();
                    t.Span(" as the reference for every payment, and keep your receipt.");
                });
            });
            page.Footer().Element(f => Footer(f, school, $"Printed {document.PrintedOn.ToString("d MMM yyyy", Money)}"));
        }))
        .WithMetadata(new DocumentMetadata { Title = $"Invoice {i.Number}", Author = school.Name })
        .GeneratePdf();
    }

    public byte[] Receipt(ReceiptDocument document, ReportBranding school)
    {
        var r = document.Receipt;
        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A5);
            page.Margin(24);
            page.DefaultTextStyle(t => t.FontSize(9).FontColor("#1d2733"));
            page.Header().Element(h => Letterhead(h, school, r.IsReversal ? "REVERSAL" : "RECEIPT",
                [("Receipt no.", r.ReceiptNumber), ("Date", r.PaidOn.ToString("d MMM yyyy", Money))], compact: true));
            page.Content().PaddingTop(10).Column(col =>
            {
                col.Spacing(10);
                col.Item().Element(c => StudentBox(c, r.StudentName, r.AdmissionNumber, r.ClassRoom, r.Term, null));
                col.Item().Border(1).BorderColor(Navy).Padding(10).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(r.IsReversal ? "AMOUNT REVERSED" : "AMOUNT RECEIVED").FontSize(7).Bold().FontColor(Navy);
                        c.Item().Text(Ngn(Math.Abs(r.Amount))).FontSize(18).Bold();
                    });
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("METHOD").FontSize(7).Bold().FontColor(Navy);
                        c.Item().Text(Method(r.Method));
                        if (r.Reference is { } reference)
                        {
                            c.Item().PaddingTop(4).Text(r.IsReversal ? "REVERSES RECEIPT" : "REFERENCE").FontSize(7).Bold().FontColor(Navy);
                            c.Item().Text(reference);
                        }
                    });
                });
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(110); });
                    void Line(string label, string value, bool bold = false)
                    {
                        Cell(t.Cell()).Text(label);
                        var text = Cell(t.Cell()).AlignRight().Text(value);
                        if (bold) text.Bold();
                    }
                    Line("For invoice", r.InvoiceNumber);
                    Line("Invoice total", Ngn(document.InvoiceTotal));
                    Line("Paid to date", Ngn(document.PaidToDate));
                    Line("Balance after this " + (r.IsReversal ? "reversal" : "payment"), Ngn(r.BalanceAfter), bold: true);
                });
                if (document.Note is { } note) col.Item().Text($"Reason: {note}").Italic();
            });
            page.Footer().Element(f => Footer(f, school, "Receipts are never altered; corrections appear as separate reversals."));
        }))
        .WithMetadata(new DocumentMetadata { Title = $"Receipt {r.ReceiptNumber}", Author = school.Name })
        .GeneratePdf();
    }

    // ---------- building blocks ----------

    private static void Letterhead(IContainer container, ReportBranding school, string title, (string Label, string Value)[] facts, bool compact = false) =>
        container.BorderBottom(2).BorderColor(Navy).PaddingBottom(8).Row(row =>
        {
            row.ConstantItem(compact ? 40 : 56).Height(compact ? 40 : 56).Image(school.Logo ?? DefaultLogo.Value).FitArea();
            row.RelativeItem().PaddingHorizontal(8).Column(c =>
            {
                c.Item().Text(school.Name.ToUpperInvariant()).FontSize(compact ? 11 : 14).Bold().FontColor(Navy);
                if (school.Motto is { } motto && !compact) c.Item().Text(motto).Italic().FontColor(Gold);
                c.Item().Text(school.Address).FontSize(7);
                c.Item().Text(string.Join("  ·  ", new[] { school.Phone, school.Email, school.Website }.Where(x => !string.IsNullOrWhiteSpace(x)))).FontSize(7);
            });
            row.ConstantItem(compact ? 120 : 160).Column(c =>
            {
                c.Item().AlignRight().Text(title).FontSize(compact ? 14 : 20).Bold().FontColor(Navy);
                foreach (var (label, value) in facts)
                    c.Item().AlignRight().Text(t => { t.Span(label + ": ").FontSize(7).FontColor(Colors.Grey.Darken1); t.Span(value).FontSize(8).Bold(); });
            });
        });

    /// <summary>The admission number is the largest thing in the box: it's what parents and bursars trace payments by.</summary>
    private static void StudentBox(IContainer container, string name, string admissionNumber, string? classRoom, string term, string? guardian) =>
        container.Background(Fill).Padding(10).Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().Text("STUDENT").FontSize(7).Bold().FontColor(Navy);
                c.Item().Text(name).FontSize(12).Bold();
                c.Item().Text(string.Join("  ·  ", new[] { classRoom, term }.Where(x => !string.IsNullOrWhiteSpace(x))));
                if (guardian is not null) c.Item().PaddingTop(2).Text(t => { t.Span("Bill to: ").FontColor(Colors.Grey.Darken1); t.Span(guardian); });
            });
            row.ConstantItem(170).AlignRight().Column(c =>
            {
                c.Item().AlignRight().Text("ADMISSION NO.").FontSize(7).Bold().FontColor(Navy);
                c.Item().AlignRight().Text(admissionNumber).FontSize(15).Bold();
            });
        });

    private static void Footer(IContainer container, ReportBranding school, string note) =>
        container.BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingTop(4).Row(row =>
        {
            row.RelativeItem().Text(note).FontSize(7).FontColor(Colors.Grey.Darken1);
            row.ConstantItem(140).AlignRight().Text(school.Name).FontSize(7).FontColor(Colors.Grey.Darken1);
        });

    private static void HeadCell(IContainer cell, string text) =>
        cell.Background(Navy).PaddingVertical(4).PaddingHorizontal(5).Text(text).FontSize(7).Bold().FontColor(Colors.White);

    private static IContainer Cell(IContainer cell) =>
        cell.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).PaddingHorizontal(5);

    private static void Total(ColumnDescriptor col, string label, decimal amount, bool bold, string colour = "#1d2733") =>
        col.Item().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).Row(row =>
        {
            var l = row.RelativeItem().Text(label);
            var v = row.ConstantItem(120).AlignRight().Text(Ngn(amount)).FontColor(colour);
            if (bold) { l.Bold(); v.Bold(); }
        });

    private static string Ngn(decimal amount) => (amount < 0 ? "-NGN " : "NGN ") + Math.Abs(amount).ToString("#,##0.00", Money);

    private static string Method(PaymentMethod method) => method switch
    {
        PaymentMethod.BankTransfer => "Bank transfer",
        PaymentMethod.Pos => "POS",
        PaymentMethod.Online => "Online (Paystack)",
        _ => "Cash",
    };

    private static string Status(InvoiceStatus status) => status switch
    {
        InvoiceStatus.PartiallyPaid => "Part paid",
        _ => status.ToString(),
    };
}
