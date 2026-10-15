using Maydel.Application.Common.Messaging;
using Maydel.Domain.Finance;

namespace Maydel.Application.Finance.RecordPayment;

/// <summary>
/// Records a cash, bank transfer or POS payment against an invoice and issues a receipt (E10.3). <c>PaidOn</c> defaults
/// to today and can't be in the future. Full payment is required unless the term has an enabled instalment plan; with
/// one, each payment must at least complete the next instalment (400 <c>errors.amount</c>), and <c>NextMinimum</c> on the
/// invoice says how much that is.
/// Online payments arrive through Paystack, not here.
/// </summary>
public sealed record RecordPaymentCommand(Guid InvoiceId, decimal Amount, PaymentMethod Method, string? Reference, DateOnly? PaidOn) : ICommand<ReceiptDto>;
