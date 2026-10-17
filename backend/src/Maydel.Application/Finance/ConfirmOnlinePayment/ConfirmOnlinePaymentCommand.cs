using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Finance.ConfirmOnlinePayment;

/// <summary>
/// Confirms a Paystack payment by reference (webhook or return page). Idempotent. Not wrapped in a transaction so a
/// concurrent duplicate (caught by the unique index) can be answered with the already-recorded result.
/// </summary>
public sealed record ConfirmOnlinePaymentCommand(string Reference) : ICommand<OnlinePaymentResult>, INonTransactional;
