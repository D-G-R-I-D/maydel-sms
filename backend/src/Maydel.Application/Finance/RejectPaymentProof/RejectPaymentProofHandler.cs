using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance.RejectPaymentProof;

internal sealed class RejectPaymentProofHandler(IAppDbContext db, PaymentProofs proofs, ProofEmails emails, TimeProvider time, ICurrentUser me)
    : ICommandHandler<RejectPaymentProofCommand, ProofDto>
{
    public async Task<Result<ProofDto>> Handle(RejectPaymentProofCommand c, CancellationToken ct)
    {
        var proof = await db.PaymentProofs.FirstOrDefaultAsync(p => p.Id == c.ProofId, ct);
        if (proof is null) return FinanceErrors.ProofNotFound;
        if (proof.Reject(c.Reason ?? "", me.UserId, time.GetUtcNow()) is { } invalid) return invalid.ToError();
        await emails.QueueAsync(proof, confirmed: false, time.GetUtcNow(), ct);
        await db.SaveChangesAsync(ct);
        return await proofs.Dtos(db.PaymentProofs.Where(p => p.Id == proof.Id)).FirstAsync(ct);
    }
}
