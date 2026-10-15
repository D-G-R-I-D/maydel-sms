using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Finance.GetInstalmentPlan;

/// <summary>The instalment plan that applies to a term (its own, its session's, or none).</summary>
public sealed record GetInstalmentPlanQuery(Guid TermId) : IQuery<InstalmentPlanDto>;
