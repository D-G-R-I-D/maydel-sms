using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Cbt;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt.ListMyTests;

internal sealed class ListMyTestsHandler(IAppDbContext db, Attempts attempts) : IQueryHandler<ListMyTestsQuery, IReadOnlyList<StudentTestDto>>
{
    public async Task<Result<IReadOnlyList<StudentTestDto>>> Handle(ListMyTestsQuery q, CancellationToken ct)
    {
        if (await attempts.MeAsync(ct) is not { } student) return CbtErrors.NotAStudent;
        var tests = await attempts.TestsFor(student).ToListAsync(ct);
        var ids = tests.Select(t => t.Id).ToList();
        var mine = await db.TestAttempts.Where(a => a.StudentId == student.Id && ids.Contains(a.TestId)).ToListAsync(ct);
        foreach (var a in mine) await attempts.CloseIfOverdueAsync(a, ct);
        var subjects = await db.Subjects.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var now = attempts.Now;

        return tests.Select(t =>
        {
            var used = mine.Where(a => a.TestId == t.Id).ToList();
            var active = used.FirstOrDefault(a => a.Status == AttemptStatus.InProgress);
            var state = active is not null ? StudentTestState.InProgress
                : now < t.OpensAt ? StudentTestState.Upcoming
                : now < t.ClosesAt && used.Count < t.MaxAttempts ? StudentTestState.Open
                : used.Count > 0 ? StudentTestState.Done
                : StudentTestState.Missed;
            return new StudentTestDto(t.Id, t.Title, subjects[t.SubjectId], t.Instructions, t.DurationMinutes, t.OpensAt, t.ClosesAt, t.QuestionCount, t.TotalMarks,
                t.MaxAttempts, used.Count, state, active?.Id);
        })
        .OrderBy(t => t.State switch { StudentTestState.InProgress => 0, StudentTestState.Open => 1, StudentTestState.Upcoming => 2, _ => 3 })
        .ThenBy(t => t.OpensAt)
        .ToList();
    }
}
