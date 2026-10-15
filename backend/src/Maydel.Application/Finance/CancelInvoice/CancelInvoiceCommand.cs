using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Finance.CancelInvoice;

/// <summary>Cancels an invoice with no money on it (admin), e.g. billed in error or the student left. It can be re-generated.</summary>
public sealed record CancelInvoiceCommand(Guid InvoiceId, string Reason) : ICommand<InvoiceDetail>;
