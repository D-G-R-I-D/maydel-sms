using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt.ListTests;

internal sealed class ListTestsHandler(IAppDbContext db, AccessRules access) : IQueryHandler<ListTestsQuery, IReadOnlyList<TestSummary>>
{
    public async Task<Result<IReadOnlyList<TestSummary>>> Handle(ListTestsQuery q, CancellationToken ct)
    {
        var query = db.CbtTests.AsNoTracking().Where(t => t.TermId == q.TermId);
        if (q.SubjectId is { } subject) query = query.Where(t => t.SubjectId == subject);
        if (q.ClassLevelId is { } level) query = query.Where(t => t.ClassLevelId == level);
        if (q.Status is { } status) query = query.Where(t => t.Status == status);
        var tests = await query.OrderBy(t => t.OpensAt).ToListAsync(ct);

        var scope = await access.AuthoringScopeAsync(ct);
        if (scope is not null) tests = [.. tests.Where(t => scope.Contains((t.SubjectId, t.ClassLevelId)))];

        var subjects = await db.Subjects.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var levels = await db.ClassLevels.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.Name, ct);
        var components = await db.AssessmentComponents.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var roomIds = tests.SelectMany(t => t.ClassRoomIds).Distinct().ToList();
        var rooms = await db.ClassRooms.AsNoTracking().Where(r => roomIds.Contains(r.Id))
            .Select(r => new { r.Id, Name = r.ClassLevel.Name + " " + r.Arm }).ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        return tests.Select(t => new TestSummary(t.Id, t.Title, t.SubjectId, subjects[t.SubjectId], t.ClassLevelId, levels[t.ClassLevelId], t.TermId,
                t.ComponentId is { } c ? components.GetValueOrDefault(c) : null, t.Status, t.OpensAt, t.ClosesAt, t.DurationMinutes, t.QuestionCount, t.TotalMarks,
                [.. t.ClassRoomIds.Where(rooms.ContainsKey).Select(r => rooms[r]).Order()]))
            .ToList();
    }
}
