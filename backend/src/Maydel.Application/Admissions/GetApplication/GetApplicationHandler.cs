using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.GetApplication;

internal sealed class GetApplicationHandler(IAppDbContext db, ICurrentUser me) : IQueryHandler<GetApplicationQuery, ApplicationDetail>
{
    public async Task<Result<ApplicationDetail>> Handle(GetApplicationQuery q, CancellationToken ct)
    {
        if (!q.AsStaff && !await db.Applications.AnyAsync(a => a.Id == q.Id && a.ApplicantUserId == me.UserId, ct)) return AdmissionErrors.ApplicationNotFound;
        return await db.DetailAsync(q.Id, ct, q.AsStaff) is { } detail ? detail : AdmissionErrors.ApplicationNotFound;
    }
}
