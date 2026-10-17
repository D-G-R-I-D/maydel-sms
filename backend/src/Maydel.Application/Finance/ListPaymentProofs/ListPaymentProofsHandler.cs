using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance.ListPaymentProofs;

internal sealed class ListPaymentProofsHandler(IAppDbContext db, PaymentProofs proofs) : IQueryHandler<ListPaymentProofsQuery, PagedResult<ProofDto>>
{
    public async Task<Result<PagedResult<ProofDto>>> Handle(ListPaymentProofsQuery q, CancellationToken ct)
    {
        ProofStatus? status = q.All ? null : q.Status ?? ProofStatus.Pending;
        var rows = proofs.Dtos(db.PaymentProofs.AsNoTracking().Where(p => status == null || p.Status == status), oldestFirst: status == ProofStatus.Pending);
        return await rows.ToPagedAsync(q.Page, q.PageSize, ct);
    }
}
