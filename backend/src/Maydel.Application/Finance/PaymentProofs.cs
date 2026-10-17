using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Photos;
using Maydel.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance;

/// <summary>Stores proof files privately and builds proof DTOs (E10.8).</summary>
public sealed class PaymentProofs(IAppDbContext db, IFileStorage storage, PhotoStore photos)
{
    public const int MaxPdfBytes = 5 * 1024 * 1024;
    public const int MaxImageDimension = 1600;

    /// <summary>
    /// Images are re-encoded (metadata and anything hidden stripped); PDFs are kept as uploaded after a signature check
    /// and are only ever served as downloads to staff and the family. Returns (key, content type) or null when unusable.
    /// </summary>
    public async Task<(string Key, string ContentType)?> SaveAsync(byte[] upload, CancellationToken ct, string folder = "proofs")
    {
        if (upload.Length >= 5 && upload.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
        {
            if (upload.Length > MaxPdfBytes) return null;
            using var stream = new MemoryStream(upload);
            return (await storage.SaveAsync(stream, folder, "pdf", "application/pdf", ct), "application/pdf");
        }
        var saved = await photos.SaveAsync(upload, folder, transparent: false, MaxImageDimension, ct);
        return saved.IsSuccess ? (saved.Value.Key, saved.Value.Image.ContentType) : null;
    }

    public async Task<Result<PhotoContent>> OpenAsync(PaymentProof proof, CancellationToken ct) =>
        await OpenAsync(proof.FilePath, proof.ContentType, ct) is { } content ? content : FinanceErrors.ProofNotFound;

    public async Task<PhotoContent?> OpenAsync(string key, string contentType, CancellationToken ct) =>
        await storage.OpenAsync(key, ct) is { } stream ? new PhotoContent(stream, contentType, $"\"{Path.GetFileNameWithoutExtension(key)}\"") : null;

    /// <summary>Sorted by submission time before projecting (EF can't order by a record's constructor arguments).</summary>
    public IQueryable<ProofDto> Dtos(IQueryable<PaymentProof> proofs, bool oldestFirst = false)
    {
        var joined = proofs.Join(db.Invoices, p => p.InvoiceId, i => i.Id, (p, i) => new { p, i });
        joined = oldestFirst ? joined.OrderBy(x => x.p.CreatedAt) : joined.OrderByDescending(x => x.p.CreatedAt);
        return joined
            .Select(x => new ProofDto(x.p.Id, x.i.Id, x.i.Number, x.p.StudentId, x.i.Student.FirstName + " " + x.i.Student.LastName, x.i.Student.AdmissionNumber,
                x.p.Amount, x.p.PaidOn, x.p.BankReference, x.p.Note, x.p.ContentType, x.p.Status, x.p.RejectReason,
                db.Payments.Where(pay => pay.Id == x.p.PaymentId).Select(pay => pay.ReceiptNumber).FirstOrDefault(), x.p.PaymentId,
                x.p.CreatedAt, x.p.ReviewedAt, x.i.Lines.Sum(l => l.Amount) - (x.i.Payments.Sum(pay => (decimal?)pay.Amount) ?? 0)));
    }
}
