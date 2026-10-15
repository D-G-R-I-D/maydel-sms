using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Finance.SaveInstalmentPlan;

/// <summary>
/// Sets instalments for a term (<c>Scope</c> Term) or for every term of its session (<c>Session</c>); <c>None</c> removes
/// the term's own plan (and with <c>ClearSession</c>, the session's too). 2 or 3 parts adding up to 100%. When
/// <c>Enabled</c> is false fees are due in full; a Term-scoped switched-off plan (instalments may be empty) is how one term
/// opts out of its session's plan without touching the other terms. Applies to invoices generated afterwards.
/// </summary>
public sealed record SaveInstalmentPlanCommand(Guid TermId, PlanScope Scope, bool Enabled, IReadOnlyList<InstalmentInput> Instalments, bool ClearSession = false)
    : ICommand<InstalmentPlanDto>;
