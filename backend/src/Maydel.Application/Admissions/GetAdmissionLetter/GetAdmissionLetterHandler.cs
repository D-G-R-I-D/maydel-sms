using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Reports;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.GetAdmissionLetter;

internal sealed class GetAdmissionLetterHandler(IAppDbContext db, AdmissionLetters letters, ICurrentUser me) : IQueryHandler<GetAdmissionLetterQuery, PdfFile>
{
    public async Task<Result<PdfFile>> Handle(GetAdmissionLetterQuery q, CancellationToken ct)
    {
        if (!q.AsStaff && !await db.Applications.AnyAsync(a => a.Id == q.ApplicationId && a.ApplicantUserId == me.UserId, ct)) return AdmissionErrors.ApplicationNotFound;
        return await letters.LetterAsync(q.ApplicationId, ct) is { } pdf ? pdf : AdmissionErrors.NoLetter;
    }
}
