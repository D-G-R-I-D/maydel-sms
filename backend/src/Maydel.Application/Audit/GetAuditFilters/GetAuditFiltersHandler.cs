using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Audit.GetAuditFilters;

internal sealed class GetAuditFiltersHandler(IAppDbContext db) : IQueryHandler<GetAuditFiltersQuery, AuditFilters>
{
    public async Task<Result<AuditFilters>> Handle(GetAuditFiltersQuery query, CancellationToken ct)
    {
        var entityTypes = await db.AuditLogs.AsNoTracking().Select(l => l.EntityType).Distinct().OrderBy(t => t).ToListAsync(ct);
        return new AuditFilters(entityTypes, Enum.GetValues<AuditAction>());
    }
}
