using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Finance.SetFeeOptIns;

/// <summary>
/// Replaces who takes an optional item: <c>StudentIds</c> are the students who PAY it, whichever mode the item is in
/// (for an on-by-default item, everyone left out is excluded). Affects invoices generated afterwards.
/// </summary>
public sealed record SetFeeOptInsCommand(Guid FeeItemId, IReadOnlyList<Guid> StudentIds) : ICommand<IReadOnlyList<OptInStudent>>;
