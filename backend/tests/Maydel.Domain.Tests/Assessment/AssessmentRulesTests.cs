using Maydel.Domain.Assessment;
using static Maydel.Domain.Assessment.AssessmentRules;

namespace Maydel.Domain.Tests.Assessment;

public sealed class AssessmentRulesTests
{
    // The school's agreed default (2026-10-12).
    private static readonly ComponentInput[] Default =
    [
        new("Project", 10, true), new("Homefun", 5, true), new("Class Fun", 5, true),
        new("Attendance", 10, true), new("CAT", 30, true), new("Exams", 40, true),
    ];

    [Fact]
    public void Default_components_total_100() => Assert.Null(CheckComponents(Default));

    [Fact]
    public void Inactive_components_do_not_count_towards_100() =>
        Assert.Null(CheckComponents([.. Default, new ComponentInput("Old Class Fun", 10, false)]));

    [Theory]
    [InlineData(95, "assessment.components_total")]
    [InlineData(105, "assessment.components_total")]
    public void Active_components_must_total_exactly_100(decimal examsMax, string code)
    {
        var components = Default.Select(c => c.Name == "Exams" ? c with { MaxScore = examsMax - 60 } : c).ToList();
        Assert.Equal(code, CheckComponents(components)?.Code);
    }

    [Fact]
    public void Component_names_must_be_unique_and_present()
    {
        Assert.Equal("assessment.component_duplicate", CheckComponents([.. Default.SkipLast(1), new ComponentInput("cat", 40, true)])?.Code);
        Assert.Equal("assessment.component_name", CheckComponents([.. Default.SkipLast(1), new ComponentInput(" ", 40, true)])?.Code);
        Assert.Equal("assessment.no_components", CheckComponents([new ComponentInput("X", 100, false)])?.Code);
    }

    private static readonly BandInput[] Bands =
    [
        new("A", 70, 100, "Excellent"), new("B", 60, 69.99m, "Very Good"), new("C", 50, 59.99m, "Good"),
        new("D", 45, 49.99m, "Fair"), new("E", 40, 44.99m, "Pass"), new("F", 0, 39.99m, "Needs Improvement"),
    ];

    [Fact]
    public void Default_bands_are_valid() => Assert.Null(CheckBands(Bands));

    [Fact]
    public void Bands_must_start_at_zero_with_unique_minima_and_grades()
    {
        Assert.Equal("assessment.bands_start", CheckBands(Bands.SkipLast(1).ToList())?.Code);
        Assert.Equal("assessment.bands_duplicate_min", CheckBands([.. Bands, new BandInput("A+", 70, 100, "Outstanding")])?.Code);
        Assert.Equal("assessment.bands_grade", CheckBands([.. Bands, new BandInput("a", 90, 100, "Again")])?.Code);
        Assert.Equal("assessment.bands_range", CheckBands([.. Bands, new BandInput("Z", 95, 120, "Too high")])?.Code);
    }

    [Theory]
    [InlineData(69.5, "B")]   // between bands' stated maxima: still graded by minimum
    [InlineData(70, "A")]
    [InlineData(0, "F")]
    [InlineData(100, "A")]
    public void Grades_are_looked_up_by_minimum_without_gaps(decimal total, string grade)
    {
        var scale = new GradingScale(Bands.Select(b => new GradeBand { Grade = b.Grade, MinScore = b.MinScore, MaxScore = b.MaxScore, Remark = b.Remark }));
        Assert.Equal(grade, scale.BandFor(total)?.Grade);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(30, null)]
    [InlineData(12.5, null)]
    [InlineData(30.5, "CAT must be between 0 and 30.")]
    [InlineData(-1, "CAT must be between 0 and 30.")]
    [InlineData(12.345, "CAT can have at most 2 decimal places.")]
    public void Scores_stay_within_the_component_maximum(decimal score, string? error) =>
        Assert.Equal(error, CheckScore(score, new AssessmentComponent { Name = "CAT", MaxScore = 30 }));

    [Fact]
    public void Setting_scores_recomputes_total_grade_and_remark()
    {
        var scale = new GradingScale(Bands.Select(b => new GradeBand { Grade = b.Grade, MinScore = b.MinScore, MaxScore = b.MaxScore, Remark = b.Remark }));
        var score = new SubjectScore();

        score.SetScores([(Guid.NewGuid(), 25), (Guid.NewGuid(), 38.5m)], scale);

        Assert.Equal(63.5m, score.Total);
        Assert.Equal("B", score.Grade);
        Assert.Equal("Very Good", score.Remark);
    }

    [Fact]
    public void Ties_share_a_position_in_competition_ranking()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid(); var d = Guid.NewGuid();
        var ranks = GradingScale.Rank(new Dictionary<Guid, decimal> { [a] = 80, [b] = 75, [c] = 75, [d] = 60 });
        Assert.Equal([1, 2, 2, 4], new[] { ranks[a], ranks[b], ranks[c], ranks[d] });
    }
}
