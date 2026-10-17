using Maydel.Application.Common.Messaging;
using Maydel.Application.Finance;

namespace Maydel.Application.Portal.GetOnlinePayment;

/// <summary>
/// The parent's return page after Paystack: confirms the payment (if the webhook hasn't already) and reports the outcome.
/// Only for the parent's own children; anything else is 404.
/// </summary>
public sealed record GetOnlinePaymentQuery(string Reference) : IQuery<OnlinePaymentResult>;
