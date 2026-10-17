using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Finance;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Portal.ListChildProofs;

internal sealed class ListChildProofsHandler(IAppDbContext db, AccessRules access, PaymentProofs proofs) : IQueryHandler<ListChildProofsQuery, IReadOnlyList<ProofDto>>
{
    public async Task<Result<IReadOnlyList<ProofDto>>> Handle(ListChildProofsQuery q, CancellationToken ct)
    {
        if (!await access.PortalStudents().AnyAsync(s => s.Id == q.StudentId, ct)) return PortalErrors.StudentNotFound;
        return await proofs.Dtos(db.PaymentProofs.AsNoTracking().Where(p => p.StudentId == q.StudentId)).ToListAsync(ct);
    }
}
