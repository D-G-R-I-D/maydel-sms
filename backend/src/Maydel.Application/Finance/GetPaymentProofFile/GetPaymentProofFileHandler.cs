using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Photos;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance.GetPaymentProofFile;

internal sealed class GetPaymentProofFileHandler(IAppDbContext db, PaymentProofs proofs) : IQueryHandler<GetPaymentProofFileQuery, PhotoContent>
{
    public async Task<Result<PhotoContent>> Handle(GetPaymentProofFileQuery q, CancellationToken ct)
    {
        var proof = await db.PaymentProofs.AsNoTracking().FirstOrDefaultAsync(p => p.Id == q.ProofId, ct);
        return proof is null ? FinanceErrors.ProofNotFound : await proofs.OpenAsync(proof, ct);
    }
}
