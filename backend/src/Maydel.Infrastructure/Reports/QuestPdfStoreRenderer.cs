using System.Globalization;
using Maydel.Application.Abstractions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Maydel.Infrastructure.Reports;

/// <summary>Shop receipts (A5, the school's letterhead) and student ID cards (CR80, 85.6 × 54 mm).</summary>
internal sealed class QuestPdfStoreRenderer : IStoreDocumentRenderer
{
    private static readonly Lazy<byte[]> DefaultLogo = new(() =>
    {
        using var stream = typeof(QuestPdfStoreRenderer).Assembly.GetManifestResourceStream("Maydel.ReportLogo.png")
            ?? throw new InvalidOperationException("Report logo resource missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    });

    private const string Navy = "#234a6d";
    private const string Gold = "#d9b16a";
    private const string Fill = "#ebf1f7";
    private const string Text = "#1d2733";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // A CR80 card in points (1 mm = 2.8346 pt).
    private const float CardWidth = 242.6f;
    private const float CardHeight = 153f;

    static QuestPdfStoreRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    private static string Ngn(decimal amount) => "NGN " + amount.ToString("#,##0.00", Inv);

    public byte[] Receipt(StoreReceiptDocument r, ReportBranding school) => ReceiptDocument(r, school).GeneratePdf();

    public byte[] IdCards(IReadOnlyList<IdCardDocument> cards, ReportBranding school, bool sheet) => IdCardsDocument(cards, school, sheet).GeneratePdf();

    internal static IDocument ReceiptDocument(StoreReceiptDocument r, ReportBranding school) =>
        Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A5);
            page.Margin(24);
            page.DefaultTextStyle(t => t.FontSize(9).FontColor(Text));
            var stamp = r.IsCancelled ? "CANCELLED" : r.IsPaid ? null : "UNPAID";
            if (stamp is not null) page.Background().AlignCenter().AlignMiddle().Rotate(-30).Text(stamp).FontSize(64).Bold().FontColor("#e6edf3");

            page.Header().BorderBottom(2).BorderColor(Navy).PaddingBottom(8).Row(row =>
            {
                row.ConstantItem(40).Height(40).Image(school.Logo ?? DefaultLogo.Value).FitArea();
                row.RelativeItem().PaddingHorizontal(8).Column(c =>
                {
                    c.Item().Text(school.Name.ToUpperInvariant()).FontSize(11).Bold().FontColor(Navy);
                    c.Item().Text(school.Address).FontSize(7);
                    c.Item().Text(string.Join("  ·  ", new[] { school.Phone, school.Email }.Where(x => !string.IsNullOrWhiteSpace(x)))).FontSize(7);
                });
                row.ConstantItem(130).Column(c =>
                {
                    c.Item().AlignRight().Text("STORE RECEIPT").FontSize(13).Bold().FontColor(Navy);
                    c.Item().AlignRight().Text(t => { t.Span("Order: ").FontSize(7).FontColor(Colors.Grey.Darken1); t.Span(r.OrderNumber).FontSize(8).Bold(); });
                    c.Item().AlignRight().Text(t => { t.Span("Date: ").FontSize(7).FontColor(Colors.Grey.Darken1); t.Span(r.Date.ToOffset(TimeSpan.FromHours(1)).ToString("d MMM yyyy", Inv)).FontSize(8).Bold(); });
                });
            });

            page.Content().PaddingTop(10).Column(col =>
            {
                col.Spacing(10);
                col.Item().Background(Fill).Padding(10).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("STUDENT").FontSize(7).Bold().FontColor(Navy);
                        c.Item().Text(r.StudentName).FontSize(12).Bold();
                        if (r.ClassRoom is { } room) c.Item().Text(room);
                    });
                    row.ConstantItem(120).AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text("ADMISSION NO.").FontSize(7).Bold().FontColor(Navy);
                        c.Item().AlignRight().Text(r.AdmissionNumber).FontSize(12).Bold();
                    });
                });
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(30); c.ConstantColumn(88); c.ConstantColumn(92); });
                    t.Header(h =>
                    {
                        foreach (var (label, right) in new[] { ("ITEM", false), ("QTY", true), ("PRICE", true), ("AMOUNT", true) })
                        {
                            var cell = h.Cell().Background(Navy).PaddingVertical(4).PaddingHorizontal(6);
                            (right ? cell.AlignRight() : cell).Text(label).FontSize(7).Bold().FontColor(Colors.White);
                        }
                    });
                    foreach (var l in r.Lines)
                    {
                        IContainer Cell() => t.Cell().BorderBottom(1).BorderColor("#d6e1ec").PaddingVertical(4).PaddingHorizontal(6);
                        Cell().Text($"{l.Item} ({l.Option})");
                        Cell().AlignRight().Text(l.Quantity.ToString(Inv));
                        Cell().AlignRight().Text(Ngn(l.UnitPrice));
                        Cell().AlignRight().Text(Ngn(l.LineTotal));
                    }
                });
                col.Item().AlignRight().Width(200).Row(row =>
                {
                    row.RelativeItem().Text("TOTAL").Bold().FontColor(Navy);
                    row.RelativeItem().AlignRight().Text(Ngn(r.Total)).FontSize(13).Bold();
                });
                col.Item().Border(1).BorderColor(Navy).Padding(8).Column(c =>
                {
                    c.Item().Text(t => { t.Span("Payment: ").Bold(); t.Span(r.PaymentStatus); });
                    if (r.PaidWith is { } method) c.Item().Text(t => { t.Span("Method: ").Bold(); t.Span(method); });
                    if (r.Reference is { } reference) c.Item().Text(t => { t.Span("Reference: ").Bold(); t.Span(reference); });
                    if (r.InvoiceNumber is { } invoice) c.Item().Text(t => { t.Span("Added to invoice: ").Bold(); t.Span(invoice); });
                });
            });
            page.Footer().AlignCenter().Text("Please keep this receipt.").FontSize(7).FontColor(Colors.Grey.Darken1);
        }))
        .WithMetadata(new DocumentMetadata { Title = $"Receipt {r.OrderNumber}", Author = school.Name });

    internal static IDocument IdCardsDocument(IReadOnlyList<IdCardDocument> cards, ReportBranding school, bool sheet) =>
        Document.Create(doc =>
        {
            if (!sheet)
            {
                foreach (var card in cards)
                    doc.Page(page =>
                    {
                        page.Size(new PageSize(CardWidth, CardHeight));
                        page.Margin(0);
                        page.Content().Element(c => Card(c, card, school));
                    });
                return;
            }
            // Ten cards per A4 sheet (2 × 5) with thin grey cut lines.
            foreach (var chunk in cards.Chunk(10))
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginVertical(28);
                    page.MarginHorizontal(40);
                    page.Content().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.ConstantColumn(CardWidth + 8); c.ConstantColumn(CardWidth + 8); });
                        foreach (var card in chunk)
                            t.Cell().Padding(4).Border(0.5f).BorderColor("#c9d3dd").Width(CardWidth).Height(CardHeight).Element(c => Card(c, card, school));
                    });
                });
        })
        .WithMetadata(new DocumentMetadata { Title = "Student ID cards", Author = school.Name });

    private static void Card(IContainer container, IdCardDocument card, ReportBranding school) =>
        container.Background(Colors.White).Column(col =>
        {
            col.Item().Height(34).Background(Navy).PaddingHorizontal(8).Row(row =>
            {
                row.ConstantItem(26).AlignMiddle().Height(26).Background(Colors.White).Padding(1).Image(school.Logo ?? DefaultLogo.Value).FitArea();
                row.RelativeItem().PaddingLeft(6).AlignMiddle().Column(c =>
                {
                    c.Item().Text(school.Name.ToUpperInvariant()).FontSize(8).Bold().FontColor(Colors.White);
                    c.Item().Text("STUDENT IDENTITY CARD").FontSize(5.5f).FontColor(Gold).LetterSpacing(0.08f);
                });
            });
            col.Item().Height(2).Background(Gold);
            col.Item().PaddingHorizontal(8).PaddingTop(6).Row(row =>
            {
                var photo = row.ConstantItem(62).Height(78).Border(0.75f).BorderColor(Navy);
                if (card.Photo is { } bytes) photo.Image(bytes).FitArea();
                else photo.Background(Fill).AlignCenter().AlignMiddle().Text("PHOTO").FontSize(6).FontColor(Navy);

                row.RelativeItem().PaddingLeft(8).Column(c =>
                {
                    c.Spacing(1.5f);
                    c.Item().Text(card.StudentName).FontSize(10).Bold().FontColor(Text);
                    void Field(string label, string value)
                    {
                        c.Item().PaddingTop(2).Text(label).FontSize(5.5f).Bold().FontColor(Navy);
                        c.Item().Text(value).FontSize(8).FontColor(Text);
                    }
                    Field("ADMISSION NO.", card.AdmissionNumber);
                    if (card.ClassRoom is { } room) Field("CLASS", room);
                    Field("SESSION", $"{card.Session} · valid until {card.ValidUntil.ToString("d MMM yyyy", Inv)}");
                });
            });
            col.Item().ExtendVertical().AlignBottom().Background(Fill).PaddingVertical(3).PaddingHorizontal(8)
                .Text($"If found, please return to {school.Name}{(string.IsNullOrWhiteSpace(school.Phone) ? "" : " · " + school.Phone)}").FontSize(5.5f).FontColor(Navy);
        });
}
