using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Finance;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Portal.GetChildFees;

internal sealed class GetChildFeesHandler(IAppDbContext db, AccessRules access, ISchoolClock clock) : IQueryHandler<GetChildFeesQuery, PortalFees>
{
    public async Task<Result<PortalFees>> Handle(GetChildFeesQuery q, CancellationToken ct)
    {
        if (!await access.PortalStudents().AnyAsync(s => s.Id == q.StudentId, ct)) return PortalErrors.StudentNotFound;
        var ids = await db.Invoices.AsNoTracking().Where(i => i.StudentId == q.StudentId && !i.IsCancelled)
            .OrderByDescending(i => i.CreatedAt).Select(i => i.Id).ToListAsync(ct);
        var invoices = new List<InvoiceDetail>(ids.Count);
        foreach (var id in ids) invoices.Add((await db.DetailAsync(id, clock.Today, ct))!);
        return new PortalFees(q.StudentId, invoices.Sum(i => i.Balance), invoices.Sum(i => i.Overdue), invoices);
    }
}
