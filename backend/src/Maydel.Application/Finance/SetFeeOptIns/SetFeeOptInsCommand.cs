using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Finance.SetFeeOptIns;

/// <summary>Replaces who takes an optional item (e.g. bus). Affects invoices generated afterwards.</summary>
public sealed record SetFeeOptInsCommand(Guid FeeItemId, IReadOnlyList<Guid> StudentIds) : ICommand<IReadOnlyList<OptInStudent>>;
