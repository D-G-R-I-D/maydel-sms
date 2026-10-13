using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Academics.GetCurrentTerm;

internal sealed class GetCurrentTermHandler(IAppDbContext db) : IQueryHandler<GetCurrentTermQuery, TermDto>
{
    public async Task<Result<TermDto>> Handle(GetCurrentTermQuery query, CancellationToken ct) =>
        await db.Terms.AsNoTracking().Where(t => t.IsCurrent).ToDtos().FirstOrDefaultAsync(ct) is { } term
            ? term
            : AcademicsErrors.NoCurrentTerm;
}
