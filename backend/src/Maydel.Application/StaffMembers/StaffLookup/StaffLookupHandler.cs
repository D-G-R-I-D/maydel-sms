using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.StaffMembers.StaffLookup;

internal sealed class StaffLookupHandler(IAppDbContext db) : IQueryHandler<StaffLookupQuery, IReadOnlyList<StaffLookupItem>>
{
    public async Task<Result<IReadOnlyList<StaffLookupItem>>> Handle(StaffLookupQuery query, CancellationToken ct) =>
        await db.Staff.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.FullName)
            .Select(s => new StaffLookupItem(s.Id, s.FullName, s.JobTitle)).ToListAsync(ct);
}
