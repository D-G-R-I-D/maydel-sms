using Maydel.Application.Common.Messaging;
using Maydel.Domain.Finance;

namespace Maydel.Application.Finance.ConfirmPaymentProof;

/// <summary>
/// The bursar has seen the money in the bank: records the payment (the amount on the proof unless corrected), issues a
/// receipt linked to the proof and emails the parent the receipt (E10.8). The instalment minimum doesn't apply.
/// </summary>
public sealed record ConfirmPaymentProofCommand(Guid ProofId, decimal? Amount, PaymentMethod? Method, DateOnly? PaidOn) : ICommand<ProofDto>;
