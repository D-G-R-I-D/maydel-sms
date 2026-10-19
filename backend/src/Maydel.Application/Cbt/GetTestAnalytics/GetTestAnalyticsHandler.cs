using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Assessment;
using Maydel.Domain.Cbt;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt.GetTestAnalytics;

internal sealed class GetTestAnalyticsHandler(IAppDbContext db, Tests tests, Results results) : IQueryHandler<GetTestAnalyticsQuery, TestAnalytics>
{
    public async Task<Result<TestAnalytics>> Handle(GetTestAnalyticsQuery q, CancellationToken ct)
    {
        if (await tests.ManageableAsync(q.TestId, ct, track: false) is not { } test) return CbtErrors.TestNotFound;
        var submitted = (await results.AttemptsAsync(test.Id, ct)).Where(a => a.Status == AttemptStatus.Submitted).ToList();

        // Scores: best fully-marked attempt per student.
        var best = submitted.GroupBy(a => a.StudentId).Select(g => Attempts.Best(g)).OfType<TestAttempt>().ToList();
        var total = test.TotalMarks;
        var scores = best.Select(a => a.TotalScore!.Value).Order().ToList();
        decimal? Median() => scores.Count == 0 ? null : scores.Count % 2 == 1 ? scores[scores.Count / 2] : ReportMath.Round((scores[scores.Count / 2 - 1] + scores[scores.Count / 2]) / 2);
        var bands = Enumerable.Range(0, 10).Select(b => new ScoreBand(b * 10, b * 10 + 10,
            scores.Count(s => Band(s, total) == b))).ToList();

        var studentIds = best.Select(a => a.StudentId).ToList();
        var rooms = await db.Students.AsNoTracking().Where(s => studentIds.Contains(s.Id))
            .Select(s => new { s.Id, Room = s.ClassRoom == null ? "No class" : s.ClassRoom.ClassLevel.Name + " " + s.ClassRoom.Arm }).ToDictionaryAsync(s => s.Id, s => s.Room, ct);
        var byClass = best.GroupBy(a => rooms.GetValueOrDefault(a.StudentId, "No class")).OrderBy(g => g.Key)
            .Select(g => new ClassResult(g.Key, g.Count(), ReportMath.Round(g.Average(a => a.TotalScore!.Value)))).ToList();

        // Items: every handed-in attempt.
        var questionIds = submitted.SelectMany(a => a.Items).Select(i => i.QuestionId).Distinct().ToList();
        var questions = await db.Questions.AsNoTracking().Where(x => questionIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var sections = test.Sections.ToDictionary(s => s.Id, s => (s.Order, s.Title));
        var items = submitted.SelectMany(a => a.Items).GroupBy(i => i.QuestionId).Where(g => questions.ContainsKey(g.Key)).Select(g =>
        {
            var q = questions[g.Key];
            var list = g.ToList();
            var scored = list.Where(i => i.Score is not null && i.Marks > 0).ToList();
            var section = sections.GetValueOrDefault(list[0].SectionId, (99, ""));
            return (section.Item1, new ItemAnalysis(q.Id, q.Kind, q.Text.Length > 200 ? q.Text[..200] + "…" : q.Text, section.Item2, list[0].Marks, list.Count,
                list.Count(i => i.AnsweredAt is not null),
                scored.Count == 0 ? null : Math.Round(scored.Average(i => i.Score!.Value / i.Marks), 2),
                [.. q.Options.OrderBy(o => o.Order).Select(o => new OptionPicks(o.Id, o.Text, o.IsCorrect, list.Count(i => i.AnswerOptionIds.Contains(o.Id))))]));
        }).OrderBy(x => x.Item1).ThenBy(x => x.Item2.Facility ?? 1).Select(x => x.Item2).ToList();

        return new TestAnalytics(test.Id, total, scores.Count, scores.Count == 0 ? null : ReportMath.Round(scores.Average()), Median(),
            scores.Count == 0 ? null : scores[^1], scores.Count == 0 ? null : scores[0], bands, byClass, items);
    }

    /// <summary>0–9: which tenth of the marks a score falls in (full marks go in the top band).</summary>
    private static int Band(decimal score, decimal total) => total <= 0 ? 0 : Math.Min(9, (int)Math.Floor(score / total * 10));
}
