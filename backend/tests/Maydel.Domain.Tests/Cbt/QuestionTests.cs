using Maydel.Domain.Cbt;

namespace Maydel.Domain.Tests.Cbt;

public sealed class QuestionTests
{
    private static Question Objective() => new()
    {
        Kind = QuestionKind.Objective, Text = " What is 2 + 2? ",
        Options = [new() { Text = "3" }, new() { Text = "4", IsCorrect = true }, new() { Text = "5" }],
    };

    [Fact]
    public void Objective_needs_two_or_more_distinct_options_and_exactly_one_answer()
    {
        var q = Objective();
        Assert.Null(q.Normalise());
        Assert.Equal(("What is 2 + 2?", 2), (q.Text, q.Options[2].Order));

        q.Options[0].IsCorrect = true;
        Assert.Equal("options", q.Normalise()?.Field);
        q.Options[0].IsCorrect = false;
        q.Options[2].Text = "4";
        Assert.Equal("question.options", q.Normalise()?.Code);
        Assert.Equal("options", new Question { Kind = QuestionKind.Objective, Text = "x", Options = [new() { Text = "a", IsCorrect = true }] }.Normalise()?.Field);
    }

    [Theory]
    [InlineData(QuestionKind.TrueFalse, "trueFalseAnswer")]
    [InlineData(QuestionKind.FillInTheGap, "text")]
    [InlineData(QuestionKind.Theory, "markingGuide")]
    public void Each_kind_needs_its_own_answer(QuestionKind kind, string field) =>
        Assert.Equal(field, new Question { Kind = kind, Text = "The capital of Nigeria is Abuja." }.Normalise()?.Field);

    [Fact]
    public void A_gap_question_needs_exactly_one_gap_and_an_accepted_answer()
    {
        var q = new Question { Kind = QuestionKind.FillInTheGap, Text = "The capital of Nigeria is ___.", Options = [new() { Text = "stray" }] };
        Assert.Equal("acceptedAnswers", q.Normalise()?.Field);
        q.AcceptedAnswers = ["Abuja", " abuja ", ""];
        Assert.Null(q.Normalise());
        Assert.Equal(["Abuja"], q.AcceptedAnswers);
        Assert.Empty(q.Options);
        q.Text = "___ is the capital of ___.";
        Assert.Equal("question.gap", q.Normalise()?.Code);
    }

    [Fact]
    public void Auto_marking_gives_all_or_nothing_and_leaves_theory_to_the_teacher()
    {
        var objective = Objective();
        objective.Normalise();
        var right = objective.Options.Single(o => o.IsCorrect).Id;
        Assert.Equal(1, objective.AutoMark(new QuestionAnswer([right])));
        Assert.Equal(0, objective.AutoMark(new QuestionAnswer([right, objective.Options[0].Id])));
        Assert.Equal(0, objective.AutoMark(null));

        var multi = new Question
        {
            Kind = QuestionKind.MultiSelect, Text = "Pick the even numbers",
            Options = [new() { Text = "2", IsCorrect = true }, new() { Text = "3" }, new() { Text = "4", IsCorrect = true }],
        };
        multi.Normalise();
        Assert.Equal(1, multi.AutoMark(new QuestionAnswer([multi.Options[2].Id, multi.Options[0].Id])));
        Assert.Equal(0, multi.AutoMark(new QuestionAnswer([multi.Options[0].Id])));

        var gap = new Question { Kind = QuestionKind.FillInTheGap, Text = "Water boils at ___ degrees Celsius.", AcceptedAnswers = ["100", "one hundred"] };
        gap.Normalise();
        Assert.Equal(1, gap.AutoMark(new QuestionAnswer(Text: "  One   Hundred. ")));
        Assert.Equal(0, gap.AutoMark(new QuestionAnswer(Text: "90")));

        Assert.Equal(1, new Question { Kind = QuestionKind.TrueFalse, Text = "x", TrueFalseAnswer = false }.AutoMark(new QuestionAnswer(TrueFalse: false)));
        Assert.Null(new Question { Kind = QuestionKind.Theory, Text = "Explain", MarkingGuide = "g" }.AutoMark(new QuestionAnswer(Text: "a")));
        Assert.Null(new Question { Kind = QuestionKind.ShortAnswer, Text = "Name a noun" }.AutoMark(new QuestionAnswer(Text: "dog")));
    }
}
