using Maydel.Domain.Cbt;

namespace Maydel.Domain.Tests.Cbt;

public sealed class AttemptTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 7, 0, 0, TimeSpan.Zero);

    private static Question Objective(string text) => new()
    {
        Kind = QuestionKind.Objective, Text = text,
        Options = [new() { Text = "right", IsCorrect = true, Order = 0 }, new() { Text = "wrong", Order = 1 }, new() { Text = "also wrong", Order = 2 }],
    };

    private static (CbtTest Test, Dictionary<Guid, Question> Bank) Setup()
    {
        var fixedQ = Objective("fixed");
        var theory = new Question { Kind = QuestionKind.Theory, Text = "Explain", MarkingGuide = "g" };
        var pool = Enumerable.Range(0, 6).Select(i => Objective($"pool {i}")).ToList();
        var test = new CbtTest
        {
            Title = "CAT", DurationMinutes = 30, OpensAt = Now.AddHours(-1), ClosesAt = Now.AddHours(2),
            Sections =
            [
                new() { Title = "A", Kind = QuestionKind.Objective, MarksPerQuestion = 2, QuestionIds = [fixedQ.Id] },
                new() { Title = "B", Kind = QuestionKind.Objective, MarksPerQuestion = 1, Mode = SectionMode.Random, RandomCount = 3, Order = 1 },
                new() { Title = "C", Kind = QuestionKind.Theory, MarksPerQuestion = 10, QuestionIds = [theory.Id], Order = 2 },
            ],
        };
        var bank = new[] { fixedQ, theory }.Concat(pool).ToDictionary(q => q.Id);
        return (test, bank);
    }

    private static TestAttempt Start(CbtTest test, Dictionary<Guid, Question> bank, Random random) => new()
    {
        SessionToken = "t1", StartedAt = Now, DeadlineAt = Now.AddMinutes(test.DurationMinutes),
        Items = Paper.Build(test, bank, s => [.. bank.Values.Where(q => q.Kind == s.Kind)], random),
    };

    [Fact]
    public void Papers_keep_section_order_draw_without_repeats_and_shuffle_per_student()
    {
        var (test, bank) = Setup();
        var a = Start(test, bank, new Random(1));
        var b = Start(test, bank, new Random(2));
        Assert.Equal(5, a.Items.Count);
        Assert.Equal(test.Sections.Select(s => s.Id), a.Items.Select(i => i.SectionId).Distinct());
        Assert.Equal(5, a.Items.Select(i => i.QuestionId).Distinct().Count());
        Assert.DoesNotContain(a.Items.Skip(1).Take(3), i => i.QuestionId == test.Sections[0].QuestionIds[0]); // the fixed one isn't drawn again
        Assert.Equal(new[] { 2m, 1m, 1m, 1m, 10m }, a.Items.Select(i => i.Marks));
        Assert.True(a.Items.Zip(b.Items).Any(p => p.First.QuestionId != p.Second.QuestionId || !p.First.OptionOrder.SequenceEqual(p.Second.OptionOrder)));
    }

    [Fact]
    public void Answers_need_the_current_session_and_stop_at_the_deadline()
    {
        var (test, bank) = Setup();
        var attempt = Start(test, bank, new Random(3));
        var first = attempt.Items[0];
        var right = bank[first.QuestionId].Options.Single(o => o.IsCorrect).Id;

        Assert.Equal("attempt.other_session", attempt.Answer(first.Id, new QuestionAnswer([right]), "stale", Now)?.Code);
        Assert.Null(attempt.Answer(first.Id, new QuestionAnswer([right]), "t1", Now.AddMinutes(1)));
        var token = attempt.TakeOver(Now.AddMinutes(2));
        Assert.Equal(IntegrityEventKind.TakeOver, Assert.Single(attempt.Events).Kind);
        Assert.Equal("attempt.other_session", attempt.Answer(first.Id, new QuestionAnswer([right]), "t1", Now.AddMinutes(3))?.Code);
        Assert.Equal("optionIds", attempt.Answer(first.Id, new QuestionAnswer([Guid.NewGuid()]), token, Now.AddMinutes(3))?.Field);

        Assert.Null(attempt.Answer(attempt.Items[4].Id, new QuestionAnswer(Text: "Plants use light"), token, attempt.DeadlineAt.AddSeconds(10)));
        Assert.Equal("attempt.time_up", attempt.Answer(first.Id, new QuestionAnswer([right]), token, attempt.DeadlineAt.AddSeconds(20))?.Code);
        Assert.True(attempt.IsOverdue(attempt.DeadlineAt.AddSeconds(20)));
    }

    [Fact]
    public void Submitting_marks_objective_parts_and_leaves_theory_for_the_teacher()
    {
        var (test, bank) = Setup();
        var attempt = Start(test, bank, new Random(4));
        var first = attempt.Items[0];
        attempt.Answer(first.Id, new QuestionAnswer([bank[first.QuestionId].Options.Single(o => o.IsCorrect).Id]), "t1", Now);
        var wrong = attempt.Items[1];
        attempt.Answer(wrong.Id, new QuestionAnswer([bank[wrong.QuestionId].Options.First(o => !o.IsCorrect).Id]), "t1", Now);

        Assert.Null(attempt.Submit(null, attempt.DeadlineAt.AddMinutes(5), bank, automatic: true));
        Assert.Equal((AttemptStatus.Submitted, true, attempt.DeadlineAt), (attempt.Status, attempt.AutoSubmitted, attempt.SubmittedAt!.Value));
        Assert.Equal(new decimal?[] { 2, 0, 0, 0, null }, attempt.Items.Select(i => i.AutoScore));
        Assert.Equal("attempt.submitted", attempt.Submit("t1", Now, bank)?.Code);
    }
}
