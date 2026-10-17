using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.ListFormCodes;

internal sealed class ListFormCodesHandler(IAppDbContext db) : IQueryHandler<ListFormCodesQuery, PagedResult<FormCodeDto>>
{
    public async Task<Result<PagedResult<FormCodeDto>>> Handle(ListFormCodesQuery q, CancellationToken ct)
    {
        var codes = db.FormCodes.Where(f => f.SessionId == q.SessionId);
        if (q.Used is { } used) codes = codes.Where(f => (f.UsedByApplicationId != null) == used);
        return await codes.OrderByDescending(f => f.CreatedAt)
            .Select(f => new FormCodeDto(f.Id, f.Code, f.SessionId, f.AmountPaid, f.IssuedTo, f.UsedByApplicationId != null,
                db.Applications.Where(a => a.Id == f.UsedByApplicationId).Select(a => a.ReferenceNumber).FirstOrDefault(), f.CreatedAt))
            .ToPagedAsync(q.Page, q.PageSize, ct);
    }
}
