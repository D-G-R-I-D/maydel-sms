using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance.CopyFeeItems;

internal sealed class CopyFeeItemsHandler(IAppDbContext db) : ICommandHandler<CopyFeeItemsCommand, IReadOnlyList<FeeItemDto>>
{
    public async Task<Result<IReadOnlyList<FeeItemDto>>> Handle(CopyFeeItemsCommand c, CancellationToken ct)
    {
        if (c.FromTermId == c.ToTermId) return FinanceErrors.SameTerm;
        if (!await db.Terms.AnyAsync(t => t.Id == c.FromTermId, ct) || !await db.Terms.AnyAsync(t => t.Id == c.ToTermId, ct)) return FinanceErrors.TermNotFound;
        if (await db.FeeItems.AnyAsync(f => f.TermId == c.ToTermId, ct))
            return Error.Rule("fee.term_has_items", "That term already has fee items. Remove them first, or add items one by one.");

        var source = await db.FeeItems.AsNoTracking().Include(f => f.OptIns).Where(f => f.TermId == c.FromTermId).ToListAsync(ct);
        db.FeeItems.AddRange(source.Select(f => new FeeItem
        {
            Name = f.Name, Amount = f.Amount, TermId = c.ToTermId, ClassLevelId = f.ClassLevelId, IsMandatory = f.IsMandatory,
            SiblingDiscount = f.SiblingDiscount, OptIns = [.. f.OptIns.Select(o => new FeeOptIn { StudentId = o.StudentId })],
        }));
        await db.SaveChangesAsync(ct);
        return await db.FeeItems.AsNoTracking().Where(f => f.TermId == c.ToTermId).FeeItemDtos().ToListAsync(ct);
    }
}
