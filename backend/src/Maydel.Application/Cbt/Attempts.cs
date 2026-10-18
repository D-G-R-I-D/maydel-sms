using Maydel.Application.Abstractions;
using Maydel.Domain.Cbt;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt;

/// <summary>The student side of CBT: which tests they can see, their attempts, auto-submitting overdue ones, and the paper view.</summary>
public sealed class Attempts(IAppDbContext db, ICurrentUser me, ISchoolClock clock)
{
    public DateTimeOffset Now => clock.Now.ToUniversalTime();

    /// <summary>The signed-in student (active, with a class), or null.</summary>
    public Task<Student?> MeAsync(CancellationToken ct) =>
        me.UserId is { } uid ? db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == uid && s.Status == StudentStatus.Active, ct) : Task.FromResult<Student?>(null);

    /// <summary>Published tests set for the student's class.</summary>
    public IQueryable<CbtTest> TestsFor(Student student) =>
        db.CbtTests.AsNoTracking().Where(t => t.Status == TestStatus.Published && student.ClassRoomId != null && t.ClassRoomIds.Contains(student.ClassRoomId.Value));

    /// <summary>The student's own attempt (tracked), auto-submitted first if its time is up.</summary>
    public async Task<TestAttempt?> MineAsync(Guid attemptId, Student student, CancellationToken ct)
    {
        var attempt = await db.TestAttempts.FirstOrDefaultAsync(a => a.Id == attemptId && a.StudentId == student.Id, ct);
        if (attempt is not null) await CloseIfOverdueAsync(attempt, ct);
        return attempt;
    }

    /// <summary>Hands in an attempt whose time ran out (saves it). Returns whether it did.</summary>
    public async Task<bool> CloseIfOverdueAsync(TestAttempt attempt, CancellationToken ct)
    {
        if (!attempt.IsOverdue(Now)) return false;
        attempt.Submit(null, Now, await QuestionsAsync(attempt, ct), automatic: true);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyDictionary<Guid, Question>> QuestionsAsync(TestAttempt attempt, CancellationToken ct)
    {
        var ids = attempt.Items.Select(i => i.QuestionId).ToList();
        return await db.Questions.AsNoTracking().Where(q => ids.Contains(q.Id)).ToDictionaryAsync(q => q.Id, ct);
    }

    public async Task<AttemptPaper> PaperAsync(TestAttempt attempt, bool includeToken, CancellationToken ct)
    {
        var test = await db.CbtTests.AsNoTracking().FirstAsync(t => t.Id == attempt.TestId, ct);
        var questions = await QuestionsAsync(attempt, ct);
        var sectionIndex = test.Sections.OrderBy(s => s.Order).Select((s, i) => (s.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var items = attempt.Items.OrderBy(i => i.Order).Select(i =>
        {
            var q = questions[i.QuestionId];
            var options = q.Options.ToDictionary(o => o.Id, o => o.Text);
            return new PaperItem(i.Id, i.Order, sectionIndex.GetValueOrDefault(i.SectionId), q.Kind, q.Text,
                q.ImagePath is null ? null : $"/api/student/attempts/{attempt.Id}/items/{i.Id}/image", i.Marks,
                [.. i.OptionOrder.Where(options.ContainsKey).Select(o => new PaperOption(o, options[o]))],
                i.AnswerOptionIds, i.AnswerTrueFalse, i.AnswerText, i.AnsweredAt is not null);
        }).ToList();
        var now = Now;
        var remaining = attempt.Status == AttemptStatus.InProgress ? Math.Max(0, (int)(attempt.DeadlineAt - now).TotalSeconds) : 0;
        return new AttemptPaper(attempt.Id, test.Id, test.Title, test.Instructions, attempt.Status, attempt.StartedAt, attempt.DeadlineAt, now, remaining,
            includeToken && attempt.Status == AttemptStatus.InProgress ? attempt.SessionToken : null,
            [.. test.Sections.OrderBy(s => s.Order).Select((s, i) => new PaperSection(i, s.Title, s.Instructions, s.Kind, s.MarksPerQuestion))],
            items, items.Count(i => i.Answered), attempt.SubmittedAt, attempt.AutoSubmitted);
    }
}
