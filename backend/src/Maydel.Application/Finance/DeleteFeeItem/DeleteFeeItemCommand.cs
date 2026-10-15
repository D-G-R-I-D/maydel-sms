using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Finance.DeleteFeeItem;

/// <summary>Removes a fee item (and its opt-ins). Invoices already generated keep their lines.</summary>
public sealed record DeleteFeeItemCommand(Guid Id) : ICommand;
