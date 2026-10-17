using Maydel.Application.Common.Messaging;
using Maydel.Application.Finance;

namespace Maydel.Application.Portal.SubmitPaymentProof;

/// <summary>
/// A parent uploads proof of a bank transfer or deposit against their child's published invoice (E10.8): the file plus
/// amount, date paid and bank reference. The bursar checks it; nothing is recorded until then.
/// </summary>
public sealed record SubmitPaymentProofCommand(
    Guid StudentId, Guid InvoiceId, decimal Amount, DateOnly PaidOn, string? BankReference, string? Note, byte[] File) : ICommand<ProofDto>;
