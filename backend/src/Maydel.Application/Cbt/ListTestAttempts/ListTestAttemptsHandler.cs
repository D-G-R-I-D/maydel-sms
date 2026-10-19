using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Cbt;

namespace Maydel.Application.Cbt.ListTestAttempts;

internal sealed class ListTestAttemptsHandler(Tests tests, Results results) : IQueryHandler<ListTestAttemptsQuery, AttemptRows>
{
    public async Task<Result<AttemptRows>> Handle(ListTestAttemptsQuery q, CancellationToken ct)
    {
        if (await tests.ManageableAsync(q.TestId, ct, track: false) is not { } test) return CbtErrors.TestNotFound;
        var attempts = await results.AttemptsAsync(test.Id, ct);
        var byStudent = attempts.GroupBy(a => a.StudentId).ToDictionary(g => g.Key, g => g.OrderBy(a => a.Number).ToList());
        var rows = (await results.CandidatesAsync(test, byStudent.Keys, ct)).Select(c =>
        {
            var mine = byStudent.GetValueOrDefault(c.Student.Id) ?? [];
            var shown = mine.FirstOrDefault(a => a.Status == AttemptStatus.InProgress) ?? Attempts.Best(mine) ?? mine.LastOrDefault();
            var state = shown is null ? AttemptRowState.NotStarted : shown.Status == AttemptStatus.InProgress ? AttemptRowState.InProgress : AttemptRowState.Submitted;
            return new AttemptRow(c.Student.Id, c.Student.FullName, c.Student.AdmissionNumber, c.ClassRoom, shown?.Id, mine.Count, state, shown?.StartedAt,
                shown?.SubmittedAt, shown?.AutoSubmitted ?? false, shown?.Events.Count(e => e.Kind == IntegrityEventKind.Blur) ?? 0,
                shown?.Events.Count(e => e.Kind == IntegrityEventKind.TakeOver) ?? 0,
                shown?.Status == AttemptStatus.Submitted ? shown.Items.Sum(i => i.AutoScore ?? 0) : null, shown?.TotalScore, mine.Sum(a => a.NeedsMarking));
        }).ToList();
        return new AttemptRows(test.Id, test.TotalMarks, test.ResultsReleasedAt, attempts.Count(a => a.Status == AttemptStatus.InProgress),
            attempts.Sum(a => a.NeedsMarking), rows);
    }
}
