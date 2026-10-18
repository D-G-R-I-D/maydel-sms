using Maydel.Domain.Cbt;

namespace Maydel.Domain.Tests.Cbt;

public sealed class TestTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 8, 0, 0, TimeSpan.Zero);

    private static CbtTest Exam() => new()
    {
        Title = "First term exam", DurationMinutes = 90, OpensAt = Now.AddDays(1), ClosesAt = Now.AddDays(1).AddHours(3),
        Sections =
        [
            new() { Title = "Objective", Kind = QuestionKind.Objective, MarksPerQuestion = 1, Mode = SectionMode.Random, RandomCount = 40 },
            new() { Title = "True or false", Kind = QuestionKind.TrueFalse, MarksPerQuestion = 1, QuestionIds = [.. Enumerable.Range(0, 10).Select(_ => Guid.NewGuid())] },
            new() { Title = "Fill in the gap", Kind = QuestionKind.FillInTheGap, MarksPerQuestion = 2, QuestionIds = [.. Enumerable.Range(0, 10).Select(_ => Guid.NewGuid())] },
            new() { Title = "Theory", Kind = QuestionKind.Theory, MarksPerQuestion = 10, QuestionIds = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()] },
        ],
    };

    [Fact]
    public void Sections_add_up_and_the_total_scales_to_the_component()
    {
        var exam = Exam();
        Assert.Null(exam.CheckSections());
        Assert.Equal((63, 100m), (exam.QuestionCount, exam.TotalMarks));
        Assert.Equal(new[] { 0, 1, 2, 3 }, exam.Sections.Select(s => s.Order));
        Assert.Equal(40m, CbtTest.Scale(100, exam.TotalMarks, 40));
        Assert.Equal(29.6m, CbtTest.Scale(74, 100, 40));
        Assert.Equal(20.33m, CbtTest.Scale(61, 90, 30)); // 20.333… → 20.33
        Assert.Equal(0m, CbtTest.Scale(5, 0, 40));
    }

    [Fact]
    public void Bad_sections_are_reported()
    {
        var exam = Exam();
        exam.Sections[1].QuestionIds.Add(exam.Sections[2].QuestionIds[0]);
        Assert.Equal("test.duplicate", exam.CheckSections()?.Code);

        exam = Exam();
        exam.Sections[0].RandomCount = 0;
        Assert.Equal("test.random_count", exam.CheckSections()?.Code);

        exam = Exam();
        exam.Sections[3].MarksPerQuestion = 0;
        Assert.Equal("test.section_marks", exam.CheckSections()?.Code);
    }

    [Fact]
    public void The_window_must_fit_the_sitting_and_publishing_locks_the_test()
    {
        var exam = Exam();
        Assert.Null(exam.CheckSchedule());
        exam.ClosesAt = exam.OpensAt.AddMinutes(60);
        Assert.Equal("closesAt", exam.CheckSchedule()?.Field);

        exam = Exam();
        Assert.Equal("test.not_ready", exam.Publish(Now, ["Section 1 needs questions."])?.Code);
        Assert.Null(exam.Publish(Now, []));
        Assert.Equal("test.locked", exam.CanEdit()?.Code);
        Assert.Equal("test.started", exam.Unpublish(anyAttempts: true)?.Code);
        Assert.Null(exam.Unpublish(anyAttempts: false));
        Assert.Null(exam.CanEdit());

        exam.ClosesAt = Now.AddMinutes(-1);
        exam.OpensAt = Now.AddHours(-3);
        Assert.Equal("closesAt", exam.Publish(Now, [])?.Field);
    }
}
