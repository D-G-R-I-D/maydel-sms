using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Finance.GetFeeOptIns;

/// <summary>Active students the optional item can apply to (its class level, or everyone); <c>OptedIn</c> = they pay it.</summary>
public sealed record GetFeeOptInsQuery(Guid FeeItemId) : IQuery<IReadOnlyList<OptInStudent>>;
