using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Finance.ReversePayment;

/// <summary>Reverses a payment entered in error (admin): a new negative entry with its own number; the original stays.</summary>
public sealed record ReversePaymentCommand(Guid PaymentId, string Reason) : ICommand<ReceiptDto>;
