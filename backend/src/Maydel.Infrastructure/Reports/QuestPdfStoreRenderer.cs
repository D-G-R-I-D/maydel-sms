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

    internal static IDocument IdCardsDocument(IReadOnlyList<IdCardDocument> cards, ReportBranding school, bool sheet)
    {
        var crest = school.Logo ?? DefaultLogo.Value;
        var watermark = Faded(crest);
        return Document.Create(doc =>
        {
            if (!sheet)
            {
                // One card: page 1 the front, page 2 the back.
                foreach (var card in cards)
                {
                    doc.Page(page => { page.Size(new PageSize(CardWidth, CardHeight)); page.Margin(0); page.Content().Element(c => Front(c, card, school, crest, watermark)); });
                    doc.Page(page => { page.Size(new PageSize(CardWidth, CardHeight)); page.Margin(0); page.Content().Element(c => Back(c, card, school, watermark)); });
                }
                return;
            }
            // Ten cards per A4 sheet (2 × 5), each sheet of fronts followed by its backs with the columns swapped, so double-sided
            // printing ("flip on long edge") puts every back behind its own front.
            foreach (var chunk in cards.Chunk(10))
            {
                Sheet(doc, "FRONT · Print double-sided on A4, flip on long edge, actual size (100%). Cut along the grey lines.",
                    [.. chunk.Select(card => (Action<IContainer>?)(c => Front(c, card, school, crest, watermark)))]);
                var backs = new List<Action<IContainer>?>();
                for (var row = 0; row < chunk.Length; row += 2)
                {
                    var left = chunk[row];
                    var right = row + 1 < chunk.Length ? chunk[row + 1] : null;
                    backs.Add(right is null ? null : c => Back(c, right, school, watermark));
                    backs.Add(c => Back(c, left, school, watermark));
                }
                Sheet(doc, "BACK · This page prints on the reverse of the one before.", backs);
            }
        })
        .WithMetadata(new DocumentMetadata { Title = "Student ID cards", Author = school.Name });
    }

    private static void Sheet(IDocumentContainer doc, string note, IReadOnlyList<Action<IContainer>?> cells) =>
        doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginVertical(20);
            page.MarginHorizontal((PageSizes.A4.Width - 2 * (CardWidth + 8)) / 2); // centred exactly, so the backs line up when flipped
            page.Header().Height(14).Text(note).FontSize(6.5f).FontColor(Colors.Grey.Medium);
            page.Content().Table(t =>
            {
                t.ColumnsDefinition(c => { c.ConstantColumn(CardWidth + 8); c.ConstantColumn(CardWidth + 8); });
                foreach (var draw in cells)
                {
                    var cell = t.Cell().Padding(4).Width(CardWidth).Height(CardHeight);
                    if (draw is null) cell.Text("");
                    else draw(cell.Border(0.5f).BorderColor("#c9d3dd"));
                }
            });
        });

    private const string DeepNavy = "#183754";

    /// <summary>The crest at about 7% strength, for a watermark (PDF images have no opacity setting, so it's faded here).</summary>
    private static byte[] Faded(byte[] logo)
    {
        using var source = SkiaSharp.SKBitmap.Decode(logo);
        if (source is null) return logo;
        using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(source.Width, source.Height, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul));
        surface.Canvas.Clear(SkiaSharp.SKColors.Transparent);
        using var image0 = SkiaSharp.SKImage.FromBitmap(source);
        using var paint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.White.WithAlpha(18) };
        surface.Canvas.DrawImage(image0, 0, 0, new SkiaSharp.SKSamplingOptions(SkiaSharp.SKFilterMode.Linear), paint);
        using var image = surface.Snapshot();
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static void Watermarked(IContainer container, byte[] watermark, Action<IContainer> content) =>
        container.Background(Colors.White).Layers(layers =>
        {
            layers.Layer().AlignCenter().AlignMiddle().Height(CardHeight * 0.7f).Image(watermark).FitArea();
            content(layers.PrimaryLayer());
        });

    private static void Header(IContainer container, ReportBranding school, byte[] crest, string label) =>
        container.Column(col =>
        {
            col.Item().Height(32).Background(DeepNavy).PaddingHorizontal(8).Row(row =>
            {
                row.ConstantItem(24).AlignMiddle().Height(24).Background(Colors.White).Padding(1).Image(crest).FitArea();
                row.RelativeItem().PaddingLeft(6).AlignMiddle().Column(c =>
                {
                    c.Item().Text(school.Name.ToUpperInvariant()).FontSize(8).Bold().FontColor(Colors.White);
                    c.Item().Text(label).FontSize(5.5f).FontColor(Gold).LetterSpacing(0.08f);
                });
            });
            col.Item().Height(1.5f).Background(Gold);
        });

    private static void Front(IContainer container, IdCardDocument card, ReportBranding school, byte[] crest, byte[] watermark) =>
        Watermarked(container, watermark, body => body.Column(col =>
        {
            col.Item().Element(h => Header(h, school, crest, "STUDENT IDENTITY CARD"));
            col.Item().PaddingHorizontal(8).PaddingTop(6).Row(row =>
            {
                var photo = row.ConstantItem(60).Height(76).Border(1.25f).BorderColor(Gold).Padding(1);
                if (card.Photo is { } bytes) photo.Image(bytes).FitArea();
                else photo.Background("#eef1f4").AlignCenter().AlignMiddle().Text(Initials(card.StudentName)).FontSize(18).Bold().FontColor("#9aa7b4");

                row.RelativeItem().PaddingLeft(8).Column(c =>
                {
                    c.Spacing(1.5f);
                    c.Item().Text(card.StudentName).FontSize(10).Bold().FontColor(DeepNavy);
                    void Field(string label, string value)
                    {
                        c.Item().PaddingTop(2).Text(label).FontSize(5.5f).Bold().FontColor(Navy);
                        c.Item().Text(value).FontSize(8).FontColor(Text);
                    }
                    Field("ADMISSION NO.", card.AdmissionNumber);
                    if (card.ClassRoom is { } room) Field("CLASS", room);
                    Field("SESSION", card.Session);
                });
            });
            col.Item().ExtendVertical().AlignBottom().Column(f =>
            {
                f.Item().Height(1).Background(Gold);
                f.Item().PaddingVertical(3).PaddingHorizontal(8).Row(r =>
                {
                    r.RelativeItem().Text(school.Motto ?? "").FontSize(5.5f).Italic().FontColor(Gold);
                    r.AutoItem().Text($"Valid until {card.ValidUntil.ToString("d MMM yyyy", Inv)}").FontSize(5.5f).FontColor(Navy);
                });
            });
        }));

    private static void Back(IContainer container, IdCardDocument card, ReportBranding school, byte[] watermark) =>
        Watermarked(container, watermark, body => body.Column(col =>
        {
            col.Item().Height(3).Background(DeepNavy);
            col.Item().Height(1.5f).Background(Gold);
            col.Item().PaddingHorizontal(10).PaddingTop(7).Column(c =>
            {
                c.Spacing(2);
                c.Item().Text(school.Name).FontSize(8.5f).Bold().FontColor(DeepNavy);
                if (!string.IsNullOrWhiteSpace(school.Address)) c.Item().Text(school.Address).FontSize(6.5f).FontColor(Text);
                var contacts = string.Join("  ·  ", new[] { school.Phone, school.Email }.Where(x => !string.IsNullOrWhiteSpace(x)));
                if (contacts.Length > 0) c.Item().Text(contacts).FontSize(6.5f).FontColor(Text);
                c.Item().PaddingTop(4).Text(t =>
                {
                    t.Span("If found, please return to the school").FontSize(6.5f).Bold().FontColor(Navy);
                    if (!string.IsNullOrWhiteSpace(school.Phone)) t.Span($" or call {school.Phone}").FontSize(6.5f).Bold().FontColor(Navy);
                    t.Span(".").FontSize(6.5f).Bold().FontColor(Navy);
                });
                c.Item().Text($"This card is the property of the school and is valid for the {card.Session} session only.").FontSize(5.5f).FontColor(Colors.Grey.Darken2);
            });
            col.Item().ExtendVertical().AlignBottom().PaddingHorizontal(10).PaddingBottom(6).Row(row =>
            {
                row.RelativeItem().AlignBottom().Column(c =>
                {
                    c.Item().Text("ADMISSION NO.").FontSize(5).Bold().FontColor(Navy);
                    c.Item().Text(card.AdmissionNumber).FontSize(11).Bold().FontColor(DeepNavy).LetterSpacing(0.05f);
                });
                row.ConstantItem(96).AlignBottom().Column(c =>
                {
                    if (school.Signature is { } signature) c.Item().Height(20).AlignCenter().Image(signature).FitHeight();
                    else c.Item().Height(20);
                    c.Item().BorderTop(0.6f).BorderColor(Navy).PaddingTop(1).AlignCenter()
                        .Text(string.IsNullOrWhiteSpace(school.PrincipalName) ? "Principal" : $"{school.PrincipalName}, Principal").FontSize(5.5f).FontColor(Navy);
                });
            });
        }));

    private static string Initials(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));
}
