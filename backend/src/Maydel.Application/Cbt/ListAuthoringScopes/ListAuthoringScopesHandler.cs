using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt.ListAuthoringScopes;

internal sealed class ListAuthoringScopesHandler(IAppDbContext db, AccessRules access) : IQueryHandler<ListAuthoringScopesQuery, IReadOnlyList<AuthoringScope>>
{
    public async Task<Result<IReadOnlyList<AuthoringScope>>> Handle(ListAuthoringScopesQuery q, CancellationToken ct)
    {
        var scope = await access.AuthoringScopeAsync(ct);
        // Admins: every subject offered at each level (from the class set-up).
        var pairs = scope ?? [.. (await db.ClassSubjects.AsNoTracking().Where(cs => cs.Subject.IsActive)
            .Select(cs => new { cs.SubjectId, cs.ClassRoom.ClassLevelId }).Distinct().ToListAsync(ct)).Select(p => (p.SubjectId, p.ClassLevelId))];
        var subjectIds = pairs.Select(p => p.SubjectId).Distinct().ToList();
        var levelIds = pairs.Select(p => p.ClassLevelId).Distinct().ToList();
        var subjects = await db.Subjects.AsNoTracking().Where(s => subjectIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var levels = await db.ClassLevels.AsNoTracking().Where(l => levelIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => new { l.Name, l.Order }, ct);
        var counts = (await db.Questions.AsNoTracking().Where(x => !x.IsArchived && subjectIds.Contains(x.SubjectId) && levelIds.Contains(x.ClassLevelId))
            .GroupBy(x => new { x.SubjectId, x.ClassLevelId }).Select(g => new { g.Key.SubjectId, g.Key.ClassLevelId, Count = g.Count() }).ToListAsync(ct))
            .ToDictionary(x => (x.SubjectId, x.ClassLevelId), x => x.Count);
        return pairs.Where(p => subjects.ContainsKey(p.SubjectId) && levels.ContainsKey(p.ClassLevelId))
            .OrderBy(p => levels[p.ClassLevelId].Order).ThenBy(p => subjects[p.SubjectId])
            .Select(p => new AuthoringScope(p.SubjectId, subjects[p.SubjectId], p.ClassLevelId, levels[p.ClassLevelId].Name, counts.GetValueOrDefault(p)))
            .ToList();
    }
}
