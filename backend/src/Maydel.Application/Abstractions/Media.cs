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

/// <summary>School details printed on report cards. From configuration (<c>School</c> section) until the school profile (E2.6) is editable.</summary>
public sealed class SchoolProfileOptions
{
    public const string Section = "School";
    public string Name { get; set; } = "Maydel Spring Schools, Abuja";
    public string? Motto { get; set; } = "The Well Spring of Knowledge";
    public string Address { get; set; } = "Plot D57 Cajaah Estate, Orozo, Abuja";
    public string? Phone { get; set; } = "09133595634";
    public string? Email { get; set; } = "admin@maydelspringschools.com";
    public string? Website { get; set; } = "www.maydelspringschools.com";
    /// <summary>Printed under the principal's comment.</summary>
    public string? PrincipalName { get; set; }
}

/// <summary>One report card to render: the data plus the student's photo bytes (if any).</summary>
public sealed record ReportCardDocument(Reports.ReportCard Card, byte[]? Photo);

/// <summary>Renders report cards to PDF (E5.6). One page per card; several cards make one printable document (E5.6b).</summary>
public interface IReportCardRenderer
{
    byte[] Render(IReadOnlyList<ReportCardDocument> cards);
}
