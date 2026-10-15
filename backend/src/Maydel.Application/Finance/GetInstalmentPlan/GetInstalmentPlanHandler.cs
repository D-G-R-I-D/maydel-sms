using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance.GetInstalmentPlan;

internal sealed class GetInstalmentPlanHandler(IAppDbContext db) : IQueryHandler<GetInstalmentPlanQuery, InstalmentPlanDto>
{
    public async Task<Result<InstalmentPlanDto>> Handle(GetInstalmentPlanQuery q, CancellationToken ct)
    {
        var term = await db.Terms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == q.TermId, ct);
        if (term is null) return FinanceErrors.TermNotFound;
        return await db.PlanForAsync(term, ct) is { } found
            ? new InstalmentPlanDto(term.Id, found.Scope, found.Plan.Enabled,
                [.. found.Plan.Instalments.OrderBy(i => i.Order).Select(i => new InstalmentDto(i.Order, i.Percent, i.DueOn))])
            : new InstalmentPlanDto(term.Id, PlanScope.None, false, []);
    }
}
