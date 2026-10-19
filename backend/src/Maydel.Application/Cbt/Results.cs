using Maydel.Application.Abstractions;
using Maydel.Domain.Cbt;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt;

/// <summary>Staff side of a sat test: attempts (closing overdue ones first), the marking view, and who's in the test's classes.</summary>
public sealed class Results(IAppDbContext db, Attempts attempts)
{
    /// <summary>All attempts at the test, overdue ones handed in first so the page never shows a stuck "in progress".</summary>
    public async Task<List<TestAttempt>> AttemptsAsync(Guid testId, CancellationToken ct)
    {
        var list = await db.TestAttempts.Where(a => a.TestId == testId).ToListAsync(ct);
        foreach (var a in list) await attempts.CloseIfOverdueAsync(a, ct);
        return list;
    }

    /// <summary>Active students in the test's classes plus anyone else who sat it (e.g. moved class since).</summary>
    public async Task<List<(Student Student, string? ClassRoom)>> CandidatesAsync(CbtTest test, IEnumerable<Guid> sat, CancellationToken ct)
    {
        var satIds = sat.ToList();
        var students = await db.Students.AsNoTracking()
            .Where(s => (s.Status == StudentStatus.Active && s.ClassRoomId != null && test.ClassRoomIds.Contains(s.ClassRoomId.Value)) || satIds.Contains(s.Id))
            .Select(s => new { Student = s, Room = s.ClassRoom == null ? null : s.ClassRoom.ClassLevel.Name + " " + s.ClassRoom.Arm })
            .ToListAsync(ct);
        return [.. students.OrderBy(x => x.Room).ThenBy(x => x.Student.LastName).ThenBy(x => x.Student.FirstName).Select(x => (x.Student, x.Room))];
    }

    public async Task<AttemptReview> ReviewAsync(TestAttempt attempt, CbtTest test, CancellationToken ct)
    {
        var student = await db.Students.AsNoTracking().FirstAsync(s => s.Id == attempt.StudentId, ct);
        var questions = await attempts.QuestionsAsync(attempt, ct);
        var sections = test.Sections.ToDictionary(s => s.Id, s => s.Title);
        var items = attempt.Items.OrderBy(i => i.Order).Select(i =>
        {
            var q = questions[i.QuestionId];
            var byId = q.Options.ToDictionary(o => o.Id);
            return new ReviewItem(i.Id, i.Order, sections.GetValueOrDefault(i.SectionId, ""), q.Kind, q.Id, q.Text, q.ImagePath is not null, i.Marks,
                [.. i.OptionOrder.Where(byId.ContainsKey).Select(o => new ReviewOption(o, byId[o].Text, byId[o].IsCorrect, i.AnswerOptionIds.Contains(o)))],
                q.TrueFalseAnswer, q.AcceptedAnswers, q.MarkingGuide, i.AnswerTrueFalse, i.AnswerText, i.AnsweredAt is not null,
                i.AutoScore, i.TeacherScore, i.Score, i.TeacherComment);
        }).ToList();
        return new AttemptReview(attempt.Id, test.Id, student.Id, student.FullName, student.AdmissionNumber, attempt.Number, attempt.Status, attempt.StartedAt,
            attempt.DeadlineAt, attempt.SubmittedAt, attempt.AutoSubmitted, [.. attempt.Events.OrderBy(e => e.At).Select(e => new IntegrityEventDto(e.Kind, e.At))],
            items, attempt.Items.Sum(i => i.Marks), attempt.TotalScore, attempt.NeedsMarking,
            attempt.Status == AttemptStatus.Submitted && test.ResultsReleasedAt is null);
    }
}
