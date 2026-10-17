using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Finance.SaveFeeItem;

/// <summary>
/// Creates (<c>Id</c> null) or updates a fee item for a term. Changes affect invoices generated afterwards only.
/// Making an item mandatory, or switching an optional item between off-by-default and on-by-default, clears its ticks.
/// </summary>
public sealed record SaveFeeItemCommand(Guid? Id, Guid TermId, FeeItemInput Item) : ICommand<FeeItemDto>;
