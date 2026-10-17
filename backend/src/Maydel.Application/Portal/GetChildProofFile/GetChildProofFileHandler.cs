using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Finance;
using Maydel.Application.Photos;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Portal.GetChildProofFile;

internal sealed class GetChildProofFileHandler(IAppDbContext db, AccessRules access, PaymentProofs proofs) : IQueryHandler<GetChildProofFileQuery, PhotoContent>
{
    public async Task<Result<PhotoContent>> Handle(GetChildProofFileQuery q, CancellationToken ct)
    {
        if (!await access.PortalStudents().AnyAsync(s => s.Id == q.StudentId, ct)) return PortalErrors.StudentNotFound;
        var proof = await db.PaymentProofs.AsNoTracking().FirstOrDefaultAsync(p => p.Id == q.ProofId && p.StudentId == q.StudentId, ct);
        return proof is null ? FinanceErrors.ProofNotFound : await proofs.OpenAsync(proof, ct);
    }
}
