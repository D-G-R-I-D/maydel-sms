using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance.ListFeeItems;

internal sealed class ListFeeItemsHandler(IAppDbContext db) : IQueryHandler<ListFeeItemsQuery, IReadOnlyList<FeeItemDto>>
{
    public async Task<Result<IReadOnlyList<FeeItemDto>>> Handle(ListFeeItemsQuery q, CancellationToken ct)
    {
        if (!await db.Terms.AnyAsync(t => t.Id == q.TermId, ct)) return FinanceErrors.TermNotFound;
        return await db.FeeItems.AsNoTracking().Where(f => f.TermId == q.TermId).FeeItemDtos().ToListAsync(ct);
    }
}
