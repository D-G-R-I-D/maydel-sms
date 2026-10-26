using Maydel.Application.Abstractions;
using Maydel.Infrastructure.Reports;
using QuestPDF.Fluent;
using SkiaSharp;

namespace Maydel.Api.Tests.Store;

/// <summary>The receipt and ID card layouts render (and, with MAYDEL_PRINT_PREVIEW set to a folder, are written out as PNGs to look at).</summary>
public sealed class StorePrintTests
{
    private static readonly ReportBranding School = new("Maydel Spring Schools", "Wellspring of Knowledge", "Plot 12, Abuja", "0800 000 0000",
        "info@maydel.sch.ng", "maydel.sch.ng", null, null, null);

    [Fact]
    public void Receipts_and_id_cards_render()
    {
        var receipt = QuestPdfStoreRenderer.ReceiptDocument(new StoreReceiptDocument("ORD-2026-00012", DateTimeOffset.UtcNow, "Ada Obi", "MSS/2026/0001", "Year 3 A",
            [new("Blazer (black)", "Age 7–8", 1, 18500, 18500), new("White shirt", "Age 7–8", 2, 4500, 9000)], 27500, "Paid", "POS", "POS-0042", null, true, false), School);
        var cards = Enumerable.Range(1, 4).Select(i => new IdCardDocument($"Student Number {i}", $"MSS/2026/000{i}", "Year 3 A", "2026/2027",
            new DateOnly(2027, 7, 23), i % 2 == 0 ? null : Photo())).ToList();
        var single = QuestPdfStoreRenderer.IdCardsDocument(cards[..1], School, sheet: false);
        var sheet = QuestPdfStoreRenderer.IdCardsDocument(cards, School, sheet: true);

        Assert.All(new[] { receipt, single, sheet }, d => Assert.Equal("%PDF"u8.ToArray(), d.GeneratePdf()[..4]));

        if (Environment.GetEnvironmentVariable("MAYDEL_PRINT_PREVIEW") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
            var settings = new QuestPDF.Infrastructure.ImageGenerationSettings { RasterDpi = 144 };
            foreach (var (name, doc) in new[] { ("receipt", receipt), ("id-card", single), ("id-cards-sheet", sheet) })
                File.WriteAllBytes(Path.Combine(folder, $"{name}.png"), doc.GenerateImages(settings).First());
        }
    }

    private static byte[] Photo()
    {
        using var surface = SKSurface.Create(new SKImageInfo(300, 380));
        surface.Canvas.Clear(new SKColor(0xD9, 0xE3, 0xEC));
        using var paint = new SKPaint { Color = new SKColor(0x23, 0x4A, 0x6D), IsAntialias = true };
        surface.Canvas.DrawCircle(150, 140, 70, paint);
        surface.Canvas.DrawOval(150, 360, 120, 110, paint);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        return data.ToArray();
    }
}
