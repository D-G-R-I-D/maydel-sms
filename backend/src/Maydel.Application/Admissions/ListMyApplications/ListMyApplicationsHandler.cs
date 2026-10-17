using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.ListMyApplications;

internal sealed class ListMyApplicationsHandler(IAppDbContext db, ICurrentUser me) : IQueryHandler<ListMyApplicationsQuery, IReadOnlyList<ApplicationDto>>
{
    public async Task<Result<IReadOnlyList<ApplicationDto>>> Handle(ListMyApplicationsQuery q, CancellationToken ct) =>
        await db.Dtos(db.Applications.AsNoTracking().Where(a => a.ApplicantUserId == me.UserId)).ToListAsync(ct);
}
