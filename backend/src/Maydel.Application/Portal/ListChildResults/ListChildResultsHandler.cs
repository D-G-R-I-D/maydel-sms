using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Portal.ListChildResults;

internal sealed class ListChildResultsHandler(IAppDbContext db, AccessRules access) : IQueryHandler<ListChildResultsQuery, IReadOnlyList<PortalResultSummary>>
{
    public async Task<Result<IReadOnlyList<PortalResultSummary>>> Handle(ListChildResultsQuery q, CancellationToken ct)
    {
        if (!await access.PortalStudents().AnyAsync(s => s.Id == q.StudentId, ct)) return PortalErrors.StudentNotFound;
        return await db.PublishedResults([q.StudentId]).Select(r => r.Result).ToListAsync(ct);
    }
}
