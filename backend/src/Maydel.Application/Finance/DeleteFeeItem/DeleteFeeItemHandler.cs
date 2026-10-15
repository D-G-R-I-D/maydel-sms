using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance.DeleteFeeItem;

internal sealed class DeleteFeeItemHandler(IAppDbContext db) : ICommandHandler<DeleteFeeItemCommand>
{
    public async Task<Result> Handle(DeleteFeeItemCommand c, CancellationToken ct)
    {
        var item = await db.FeeItems.FirstOrDefaultAsync(f => f.Id == c.Id, ct);
        if (item is null) return FinanceErrors.FeeItemNotFound;
        db.FeeItems.Remove(item);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
