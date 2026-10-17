using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance.SaveFeeItem;

internal sealed class SaveFeeItemHandler(IAppDbContext db) : ICommandHandler<SaveFeeItemCommand, FeeItemDto>
{
    public async Task<Result<FeeItemDto>> Handle(SaveFeeItemCommand c, CancellationToken ct)
    {
        var input = c.Item;
        if (FeeRules.CheckFeeItem(input.Name, input.Amount) is { } invalid) return invalid.ToError();
        if (!await db.Terms.AnyAsync(t => t.Id == c.TermId, ct)) return FinanceErrors.TermNotFound;
        if (input.ClassLevelId is { } level && !await db.ClassLevels.AnyAsync(l => l.Id == level, ct)) return FinanceErrors.ClassLevelNotFound;

        FeeItem? item;
        if (c.Id is { } id)
        {
            item = await db.FeeItems.Include(f => f.OptIns).FirstOrDefaultAsync(f => f.Id == id && f.TermId == c.TermId, ct);
            if (item is null) return FinanceErrors.FeeItemNotFound;
        }
        else db.FeeItems.Add(item = new FeeItem { Name = input.Name.Trim(), TermId = c.TermId });

        if (!Enum.IsDefined(input.Audience)) return Error.Validation("audience", "Choose All, NewIntake or Returning.");
        // Switching between "off by default" and "on by default" flips what the stored list means, so it starts afresh.
        if (item.IsMandatory != input.IsMandatory || item.ChargedByDefault != input.ChargedByDefault) item.OptIns.Clear();
        (item.Name, item.Amount, item.ClassLevelId, item.IsMandatory, item.SiblingDiscount, item.Audience, item.ChargedByDefault) =
            (input.Name.Trim(), input.Amount, input.ClassLevelId, input.IsMandatory, input.SiblingDiscount, input.Audience, !input.IsMandatory && input.ChargedByDefault);
        if (item.IsMandatory) item.OptIns.Clear();
        await db.SaveChangesAsync(ct);
        return await db.FeeItems.AsNoTracking().Where(f => f.Id == item.Id).FeeItemDtos().FirstAsync(ct);
    }
}
