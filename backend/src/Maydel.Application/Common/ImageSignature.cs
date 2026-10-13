namespace Maydel.Application.Common;

/// <summary>Recognises allowed image types by their first bytes (magic numbers), never by file name or declared type.</summary>
public static class ImageSignature
{
    public enum Kind { Unknown, Jpeg, Png, WebP }

    public static Kind Detect(ReadOnlySpan<byte> head)
    {
        if (head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF) return Kind.Jpeg;
        if (head.Length >= 8 && head[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return Kind.Png;
        if (head.Length >= 12 && head[..4].SequenceEqual("RIFF"u8) && head[8..12].SequenceEqual("WEBP"u8)) return Kind.WebP;
        return Kind.Unknown;
    }
}
