using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Finance.RejectPaymentProof;

/// <summary>Rejects a proof (e.g. unreadable, money not received yet) and emails the parent the reason.</summary>
public sealed record RejectPaymentProofCommand(Guid ProofId, string Reason) : ICommand<ProofDto>;
