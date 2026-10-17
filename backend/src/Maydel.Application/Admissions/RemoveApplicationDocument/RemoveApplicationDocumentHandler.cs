using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.RemoveApplicationDocument;

internal sealed class RemoveApplicationDocumentHandler(IAppDbContext db, IFileStorage storage, ICurrentUser me) : ICommandHandler<RemoveApplicationDocumentCommand, ApplicationDetail>
{
    public async Task<Result<ApplicationDetail>> Handle(RemoveApplicationDocumentCommand c, CancellationToken ct)
    {
        if (await db.MineAsync(c.ApplicationId, me.UserId, ct) is not { } application) return AdmissionErrors.ApplicationNotFound;
        if (application.CanChangeDocuments() is { } locked) return locked.ToError();
        var doc = application.Documents.FirstOrDefault(d => d.Id == c.DocumentId);
        if (doc is null) return AdmissionErrors.DocumentNotFound;
        application.Documents.Remove(doc);
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(doc.FilePath, ct);
        return (await db.DetailAsync(application.Id, ct))!;
    }
}
