namespace Maydel.Application.Abstractions;

/// <summary>
/// Private blob storage for uploaded files (photos, later documents). Keys are server-generated, e.g.
/// <c>students/0193….jpg</c>; user input never becomes part of a key. Files are never public: they are streamed
/// through API endpoints that apply the same access rules as the record they belong to.
/// Implementations: local disk (dev) and any S3-compatible store (Cloudflare R2, Railway buckets, AWS S3, MinIO).
/// </summary>
public interface IFileStorage
{
    /// <summary>Stores the content under a new key in <paramref name="folder"/> and returns the key.</summary>
    Task<string> SaveAsync(Stream content, string folder, string extension, string contentType, CancellationToken ct = default);

    /// <summary>The file, or null if it doesn't exist. The caller disposes the stream.</summary>
    Task<Stream?> OpenAsync(string key, CancellationToken ct = default);

    /// <summary>Deletes the file; missing files are ignored.</summary>
    Task DeleteAsync(string key, CancellationToken ct = default);
}

public sealed record ProcessedImage(byte[] Content, string ContentType, string Extension, int Width, int Height);

/// <summary>Decodes an untrusted upload and re-encodes it, which strips all metadata (EXIF, GPS) and any non-image payload.</summary>
public interface IImageProcessor
{
    /// <summary>
    /// A JPEG no larger than <paramref name="maxDimension"/> on its longest side, upright per EXIF orientation,
    /// or null if the bytes aren't a decodable JPEG/PNG/WebP within size limits.
    /// </summary>
    ProcessedImage? ToJpeg(ReadOnlySpan<byte> upload, int maxDimension);
}
