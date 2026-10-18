using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt.ListTopics;

internal sealed class ListTopicsHandler(IAppDbContext db, AccessRules access) : IQueryHandler<ListTopicsQuery, IReadOnlyList<string>>
{
    public async Task<Result<IReadOnlyList<string>>> Handle(ListTopicsQuery q, CancellationToken ct)
    {
        if (!await access.CanAuthorAsync(q.ClassLevelId, q.SubjectId, ct)) return CbtErrors.NotYourSubject;
        return await db.Questions.AsNoTracking()
            .Where(x => x.SubjectId == q.SubjectId && x.ClassLevelId == q.ClassLevelId && !x.IsArchived && x.Topic != null)
            .Select(x => x.Topic!).Distinct().OrderBy(t => t).ToListAsync(ct);
    }
}
