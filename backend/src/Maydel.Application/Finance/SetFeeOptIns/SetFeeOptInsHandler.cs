using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Finance.GetFeeOptIns;
using Maydel.Domain.Finance;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance.SetFeeOptIns;

internal sealed class SetFeeOptInsHandler(IAppDbContext db, IQueryHandler<GetFeeOptInsQuery, IReadOnlyList<OptInStudent>> list)
    : ICommandHandler<SetFeeOptInsCommand, IReadOnlyList<OptInStudent>>
{
    public async Task<Result<IReadOnlyList<OptInStudent>>> Handle(SetFeeOptInsCommand c, CancellationToken ct)
    {
        var item = await db.FeeItems.Include(f => f.OptIns).FirstOrDefaultAsync(f => f.Id == c.FeeItemId, ct);
        if (item is null) return FinanceErrors.FeeItemNotFound;
        if (item.IsMandatory) return FinanceErrors.MandatoryNoOptIn;

        var ids = c.StudentIds.Distinct().ToList();
        var eligible = await db.Students.Where(s => s.Status == StudentStatus.Active
            && (item.ClassLevelId == null || s.ClassRoom!.ClassLevelId == item.ClassLevelId)).Select(s => s.Id).ToListAsync(ct);
        if (ids.Except(eligible).Any()) return Error.Validation("studentIds", "Some students aren't active in this item's class level.");

        // Store the exceptions: takers when off by default, non-takers when on by default.
        var listed = item.ChargedByDefault ? eligible.Except(ids) : ids;
        item.OptIns.Clear();
        item.OptIns.AddRange(listed.Select(id => new FeeOptIn { StudentId = id }));
        await db.SaveChangesAsync(ct);
        return await list.Handle(new GetFeeOptInsQuery(c.FeeItemId), ct);
    }
}
