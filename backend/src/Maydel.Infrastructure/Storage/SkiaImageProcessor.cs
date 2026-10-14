using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using SkiaSharp;

namespace Maydel.Infrastructure.Storage;

/// <summary>
/// Decode → orient → resize → encode a fresh JPEG with SkiaSharp (MIT). Nothing from the original file survives except
/// pixels: EXIF/GPS, ICC comments and anything appended to the file are dropped.
/// </summary>
internal sealed class SkiaImageProcessor : IImageProcessor
{
    // Guards against decompression bombs: a tiny file that decodes to an enormous bitmap.
    private const long MaxSourcePixels = 50_000_000;
    private const int JpegQuality = 85;

    public ProcessedImage? ToJpeg(ReadOnlySpan<byte> upload, int maxDimension) => Render(upload, maxDimension, png: false);

    public ProcessedImage? ToPng(ReadOnlySpan<byte> upload, int maxDimension) => Render(upload, maxDimension, png: true);

    private static ProcessedImage? Render(ReadOnlySpan<byte> upload, int maxDimension, bool png)
    {
        if (ImageSignature.Detect(upload) == ImageSignature.Kind.Unknown) return null;

        using var data = SKData.CreateCopy(upload);
        using var codec = SKCodec.Create(data);
        if (codec is null) return null;
        var info = codec.Info;
        if (info.Width < 1 || info.Height < 1 || (long)info.Width * info.Height > MaxSourcePixels) return null;

        using var decoded = SKBitmap.Decode(codec, info.WithColorType(SKColorType.Rgba8888).WithAlphaType(SKAlphaType.Premul));
        if (decoded is null) return null;

        // Phones store photos sideways plus an orientation tag; apply it, because re-encoding drops the tag.
        var quarterTurns = codec.EncodedOrigin switch
        {
            SKEncodedOrigin.RightTop => 1,
            SKEncodedOrigin.BottomRight => 2,
            SKEncodedOrigin.LeftBottom => 3,
            _ => 0,
        };
        var (srcW, srcH) = quarterTurns % 2 == 1 ? (decoded.Height, decoded.Width) : (decoded.Width, decoded.Height);
        var scale = Math.Min(1.0, (double)maxDimension / Math.Max(srcW, srcH));
        var (w, h) = (Math.Max(1, (int)Math.Round(srcW * scale)), Math.Max(1, (int)Math.Round(srcH * scale)));

        using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(png ? SKColors.Transparent : SKColors.White); // JPEG: transparent areas become white, not black
        canvas.Translate(w / 2f, h / 2f);
        canvas.RotateDegrees(90 * quarterTurns);
        var (drawW, drawH) = quarterTurns % 2 == 1 ? (h, w) : (w, h);
        using var image = SKImage.FromBitmap(decoded);
        canvas.DrawImage(image, new SKRect(-drawW / 2f, -drawH / 2f, drawW / 2f, drawH / 2f),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        canvas.Flush();

        using var snapshot = surface.Snapshot();
        using var encoded = png ? snapshot.Encode(SKEncodedImageFormat.Png, 100) : snapshot.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        if (encoded is null) return null;
        return png
            ? new ProcessedImage(encoded.ToArray(), "image/png", "png", w, h)
            : new ProcessedImage(encoded.ToArray(), "image/jpeg", "jpg", w, h);
    }
}
