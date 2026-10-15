using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Finance.GetInstalmentPlan;
using Maydel.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance.SaveInstalmentPlan;

internal sealed class SaveInstalmentPlanHandler(IAppDbContext db, IQueryHandler<GetInstalmentPlanQuery, InstalmentPlanDto> get)
    : ICommandHandler<SaveInstalmentPlanCommand, InstalmentPlanDto>
{
    public async Task<Result<InstalmentPlanDto>> Handle(SaveInstalmentPlanCommand c, CancellationToken ct)
    {
        var term = await db.Terms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == c.TermId, ct);
        if (term is null) return FinanceErrors.TermNotFound;
        if (!Enum.IsDefined(c.Scope)) return Error.Validation("scope", "Choose None, Term or Session.");

        if (c.Scope == PlanScope.None)
        {
            db.InstalmentPlans.RemoveRange(await db.InstalmentPlans.Where(p => p.TermId == term.Id || (c.ClearSession && p.SessionId == term.SessionId)).ToListAsync(ct));
            await db.SaveChangesAsync(ct);
            return await get.Handle(new GetInstalmentPlanQuery(term.Id), ct);
        }

        if (FeeRules.CheckInstalments([.. c.Instalments.Select(i => (i.Percent, i.DueOn))]) is { } invalid) return invalid.ToError();

        var plan = c.Scope == PlanScope.Term
            ? await db.InstalmentPlans.Include(p => p.Instalments).FirstOrDefaultAsync(p => p.TermId == term.Id, ct)
            : await db.InstalmentPlans.Include(p => p.Instalments).FirstOrDefaultAsync(p => p.SessionId == term.SessionId, ct);
        if (plan is null)
        {
            plan = c.Scope == PlanScope.Term ? new InstalmentPlan { TermId = term.Id } : new InstalmentPlan { SessionId = term.SessionId };
            db.InstalmentPlans.Add(plan);
        }
        // A session-wide plan replaces this term's own plan, so the choice the admin just made is the one that applies.
        if (c.Scope == PlanScope.Session)
            db.InstalmentPlans.RemoveRange(await db.InstalmentPlans.Where(p => p.TermId == term.Id).ToListAsync(ct));

        plan.Enabled = c.Enabled;
        plan.Instalments.Clear();
        plan.Instalments.AddRange(c.Instalments.Select((i, n) => new Instalment { Order = n + 1, Percent = i.Percent, DueOn = i.DueOn }));
        await db.SaveChangesAsync(ct);
        return await get.Handle(new GetInstalmentPlanQuery(term.Id), ct);
    }
}
