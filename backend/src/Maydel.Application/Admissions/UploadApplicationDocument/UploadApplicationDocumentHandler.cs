using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Finance;
using Maydel.Domain.Admissions;

namespace Maydel.Application.Admissions.UploadApplicationDocument;

internal sealed class UploadApplicationDocumentHandler(IAppDbContext db, PaymentProofs files, IFileStorage storage, ICurrentUser me, TimeProvider time)
    : ICommandHandler<UploadApplicationDocumentCommand, ApplicationDetail>
{
    public async Task<Result<ApplicationDetail>> Handle(UploadApplicationDocumentCommand c, CancellationToken ct)
    {
        if (await db.MineAsync(c.ApplicationId, me.UserId, ct) is not { } application) return AdmissionErrors.ApplicationNotFound;
        if (application.CanChangeDocuments() is { } locked) return locked.ToError();
        if (!Enum.IsDefined(c.Kind)) return Error.Validation("kind", "Choose what the document is.");
        var replaced = c.Kind == DocumentKind.PassportPhoto ? application.Documents.FirstOrDefault(d => d.Kind == DocumentKind.PassportPhoto) : null;
        if (replaced is null && application.Documents.Count >= ApplicationDocument.MaxPerApplication) return AdmissionErrors.TooManyDocuments;

        if (await files.SaveAsync(c.File, ct, folder: "applications") is not { } saved)
            return Error.Validation("file", "Upload a photo (JPEG, PNG or WebP, up to 12 MB) or a PDF (up to 5 MB).");
        if (c.Kind == DocumentKind.PassportPhoto && saved.ContentType == "application/pdf")
        {
            await storage.DeleteAsync(saved.Key, ct);
            return Error.Validation("file", "The passport photograph must be a photo, not a PDF.");
        }
        if (replaced is not null)
        {
            application.Documents.Remove(replaced);
            await storage.DeleteAsync(replaced.FilePath, ct);
        }
        application.Documents.Add(new ApplicationDocument
        {
            Kind = c.Kind, FilePath = saved.Key, ContentType = saved.ContentType, UploadedAt = time.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);
        return (await db.DetailAsync(application.Id, ct))!;
    }
}
