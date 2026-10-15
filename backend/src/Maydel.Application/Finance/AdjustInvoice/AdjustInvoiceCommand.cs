using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Finance.AdjustInvoice;

/// <summary>Adds a scholarship / one-off discount (negative) or an extra charge (positive) to an invoice (admin).</summary>
public sealed record AdjustInvoiceCommand(Guid InvoiceId, string Description, decimal Amount) : ICommand<InvoiceDetail>;
