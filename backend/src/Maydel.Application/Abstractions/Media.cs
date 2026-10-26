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

    /// <summary>Like <see cref="ToJpeg"/> but a PNG that keeps transparency (logos, signatures).</summary>
    ProcessedImage? ToPng(ReadOnlySpan<byte> upload, int maxDimension);
}

/// <summary>School details printed on report cards. From configuration (<c>School</c> section) until the school profile (E2.6) is editable.</summary>
/// <summary>
/// Defaults for the school profile, copied into the database on first start-up (E2.6). After that the admin edits the
/// profile in Settings → School profile and these values are no longer read.
/// </summary>
public sealed class SchoolProfileOptions
{
    public const string Section = "School";
    public string Name { get; set; } = "Maydel Spring Schools, Abuja";
    public string? Motto { get; set; } = "The Well Spring of Knowledge";
    public string Address { get; set; } = "Plot D57 Cajaah Estate, Orozo, Abuja";
    public string? Phone { get; set; } = "09133595634";
    public string? Email { get; set; } = "admin@maydelspringschools.com";
    public string? Website { get; set; } = "www.maydelspringschools.com";
    public string? WhatsApp { get; set; } = "09133595634";
    public string? OfficeHours { get; set; } = "Monday – Friday · 8:00 AM – 4:00 PM";
    public string? InstagramUrl { get; set; } = "https://www.instagram.com/maydelsprings/";
    /// <summary>Printed under the principal's comment.</summary>
    public string? PrincipalName { get; set; }
}

/// <summary>One report card to render: the data plus the student's photo bytes (if any).</summary>
public sealed record ReportCardDocument(Reports.ReportCard Card, byte[]? Photo);

/// <summary>Renders report cards to PDF (E5.6). One page per card; several cards make one printable document (E5.6b).</summary>
/// <summary>The school's details and images for the report header and signature (E2.6). Null images use the built-in crest / no signature.</summary>
public sealed record ReportBranding(
    string Name, string? Motto, string Address, string? Phone, string? Email, string? Website, string? PrincipalName,
    byte[]? Logo, byte[]? Signature);

/// <summary>An invoice to print (E10). <c>Guardian</c>: the primary contact, shown as "Bill to".</summary>
public sealed record InvoiceDocument(Finance.InvoiceDetail Invoice, string? Guardian, DateOnly PrintedOn);

/// <summary>A receipt or reversal to print. Totals are as at this payment, so reprints never change.</summary>
public sealed record ReceiptDocument(Finance.ReceiptDto Receipt, decimal InvoiceTotal, decimal PaidToDate, string? Note, string? ReversesReceipt);

/// <summary>Renders invoices (A4) and receipts (A5) with the school's branding (E10.3).</summary>
public interface IFinanceDocumentRenderer
{
    byte[] Invoice(InvoiceDocument document, ReportBranding school);
    byte[] Receipt(ReceiptDocument document, ReportBranding school);
}

/// <summary>The admission form to print: blank (to fill by hand) when <c>Application</c> is null.</summary>
public sealed record AdmissionFormDocument(Admissions.ApplicationDetail? Application, string Session, byte[]? Photo, DateOnly PrintedOn);

/// <summary>An admission letter (E11.8). Everything printed comes from stored data, so a reprint is identical.</summary>
public sealed record AdmissionLetterDocument(
    string ReferenceNumber, string AdmissionNumber, string ChildName, string ClassLevel, string Session, string ParentName, string? HomeAddress,
    DateOnly LetterDate, DateOnly? ResumesOn);

/// <summary>A shop order's receipt (E19.3).</summary>
public sealed record StoreReceiptLine(string Item, string Option, int Quantity, decimal UnitPrice, decimal LineTotal);

public sealed record StoreReceiptDocument(string OrderNumber, DateTimeOffset Date, string StudentName, string AdmissionNumber, string? ClassRoom,
    IReadOnlyList<StoreReceiptLine> Lines, decimal Total, string PaymentStatus, string? PaidWith, string? Reference, string? InvoiceNumber, bool IsPaid, bool IsCancelled);

/// <summary>One student ID card (E19.3): photo bytes when on file, session it's for, and when it stops being valid.</summary>
public sealed record IdCardDocument(string StudentName, string AdmissionNumber, string? ClassRoom, string Session, DateOnly ValidUntil, byte[]? Photo);

public interface IStoreDocumentRenderer
{
    byte[] Receipt(StoreReceiptDocument document, ReportBranding school);

    /// <summary>One card per page when printing a single card; ten per A4 sheet (with cut lines) for a class.</summary>
    byte[] IdCards(IReadOnlyList<IdCardDocument> cards, ReportBranding school, bool sheet);
}

public interface IAdmissionLetterRenderer
{
    byte[] Render(AdmissionLetterDocument letter, ReportBranding school);
}

public interface IAdmissionFormRenderer
{
    byte[] Render(AdmissionFormDocument document, ReportBranding school);
}

public interface IReportCardRenderer
{
    byte[] Render(IReadOnlyList<ReportCardDocument> cards, ReportBranding school);
}
