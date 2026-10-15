using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Finance.SaveFeeItem;

/// <summary>
/// Creates (<c>Id</c> null) or updates a fee item for a term. Changes affect invoices generated afterwards only.
/// Making an item mandatory clears its opt-ins.
/// </summary>
public sealed record SaveFeeItemCommand(Guid? Id, Guid TermId, FeeItemInput Item) : ICommand<FeeItemDto>;
