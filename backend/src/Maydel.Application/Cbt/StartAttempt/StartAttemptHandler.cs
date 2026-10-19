using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Cbt;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt.StartAttempt;

internal sealed class StartAttemptHandler(IAppDbContext db, Attempts attempts) : ICommandHandler<StartAttemptCommand, AttemptPaper>
{
    public async Task<Result<AttemptPaper>> Handle(StartAttemptCommand c, CancellationToken ct)
    {
        if (await attempts.MeAsync(ct) is not { } student) return CbtErrors.NotAStudent;
        var test = await attempts.TestsFor(student).FirstOrDefaultAsync(t => t.Id == c.TestId, ct);
        if (test is null) return CbtErrors.TestNotFound;
        var now = attempts.Now;

        var mine = await db.TestAttempts.Where(a => a.TestId == test.Id && a.StudentId == student.Id).OrderBy(a => a.Number).ToListAsync(ct);
        foreach (var a in mine) await attempts.CloseIfOverdueAsync(a, ct);
        if (mine.FirstOrDefault(a => a.Status == AttemptStatus.InProgress) is { } active)
        {
            active.TakeOver(now);
            await db.SaveChangesAsync(ct);
            return await attempts.PaperAsync(active, includeToken: true, ct);
        }

        if (now < test.OpensAt) return Error.Rule("test.not_open", $"This test opens at {test.OpensAt:u}.");
        if (now >= test.ClosesAt) return Error.Rule("test.closed", "This test has closed.");
        if (mine.Count >= test.MaxAttempts) return Error.Rule("test.no_attempts", "You've used all your attempts at this test.");

        var fixedIds = test.Sections.SelectMany(s => s.QuestionIds).ToList();
        var fixedQuestions = await db.Questions.AsNoTracking().Where(q => fixedIds.Contains(q.Id)).ToDictionaryAsync(q => q.Id, ct);
        var pools = new Dictionary<Guid, IReadOnlyList<Question>>();
        foreach (var s in test.Sections.Where(s => s.Mode == SectionMode.Random))
        {
            var bank = db.Questions.AsNoTracking().Where(q => q.SubjectId == test.SubjectId && q.ClassLevelId == test.ClassLevelId && !q.IsArchived && q.Kind == s.Kind);
            if (s.Topic is { } topic) bank = bank.Where(q => q.Topic == topic);
            if (s.Difficulty is { } difficulty) bank = bank.Where(q => q.Difficulty == difficulty);
            pools[s.Id] = await bank.ToListAsync(ct);
        }

        var attempt = new TestAttempt
        {
            TestId = test.Id, StudentId = student.Id, Number = mine.Count + 1, StartedAt = now,
            DeadlineAt = new[] { now.AddMinutes(test.DurationMinutes), test.ClosesAt }.Min(), SessionToken = TestAttempt.NewToken(),
            Items = Paper.Build(test, fixedQuestions, s => pools[s.Id], Random.Shared),
        };
        db.TestAttempts.Add(attempt);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two "start" clicks at once: the unique (test, student, number) index let only one through.
            return Error.Conflict("attempt.starting", "The test is already starting. Refresh to continue.");
        }
        return await attempts.PaperAsync(attempt, includeToken: true, ct);
    }
}
