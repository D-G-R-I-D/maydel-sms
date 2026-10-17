using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Finance;
using Maydel.Application.Photos;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.GetApplicationDocument;

internal sealed class GetApplicationDocumentHandler(IAppDbContext db, PaymentProofs files, ICurrentUser me) : IQueryHandler<GetApplicationDocumentQuery, PhotoContent>
{
    public async Task<Result<PhotoContent>> Handle(GetApplicationDocumentQuery q, CancellationToken ct)
    {
        var application = await db.Applications.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == q.ApplicationId && (q.AsStaff || a.ApplicantUserId == me.UserId), ct);
        var doc = application?.Documents.FirstOrDefault(d => d.Id == q.DocumentId);
        if (doc is null) return AdmissionErrors.DocumentNotFound;
        return await files.OpenAsync(doc.FilePath, doc.ContentType, ct) is { } content ? content : AdmissionErrors.DocumentNotFound;
    }
}
