using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance.GetFeeOptIns;

internal sealed class GetFeeOptInsHandler(IAppDbContext db) : IQueryHandler<GetFeeOptInsQuery, IReadOnlyList<OptInStudent>>
{
    public async Task<Result<IReadOnlyList<OptInStudent>>> Handle(GetFeeOptInsQuery q, CancellationToken ct)
    {
        var item = await db.FeeItems.AsNoTracking().Include(f => f.OptIns).FirstOrDefaultAsync(f => f.Id == q.FeeItemId, ct);
        if (item is null) return FinanceErrors.FeeItemNotFound;
        var opted = item.OptIns.Select(o => o.StudentId).ToHashSet();
        return await db.Students.AsNoTracking()
            .Where(s => s.Status == StudentStatus.Active && (item.ClassLevelId == null || s.ClassRoom!.ClassLevelId == item.ClassLevelId))
            .OrderBy(s => s.ClassRoom!.ClassLevel.Order).ThenBy(s => s.ClassRoom!.Arm).ThenBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Select(s => new OptInStudent(s.Id, s.AdmissionNumber, s.LastName + ", " + s.FirstName,
                s.ClassRoom != null ? s.ClassRoom.ClassLevel.Name + " " + s.ClassRoom.Arm : null, opted.Contains(s.Id)))
            .ToListAsync(ct);
    }
}
