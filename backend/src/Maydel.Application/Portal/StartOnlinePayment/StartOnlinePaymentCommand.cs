using Maydel.Application.Common.Messaging;
using Maydel.Application.Finance;

namespace Maydel.Application.Portal.StartOnlinePayment;

/// <summary>
/// Starts a Paystack payment for the parent's child's invoice (E10.5). <c>Amount</c> defaults to the balance; a part
/// payment must at least complete the next instalment, like any other payment. Returns where to send the browser.
/// </summary>
public sealed record StartOnlinePaymentCommand(Guid StudentId, Guid InvoiceId, decimal? Amount) : ICommand<OnlineCheckout>;
