using Maydel.Domain.Assessment;

namespace Maydel.Domain.Tests.Assessment;

public sealed class TermReportTests
{
    [Fact]
    public void Published_reports_refuse_comment_and_attendance_changes_until_unpublished()
    {
        var report = new TermReport();
        Assert.Null(report.SetFormTeacherComment("  Hardworking pupil.  "));
        Assert.Equal("Hardworking pupil.", report.FormTeacherComment);

        report.Publish(72.5m, 61.2m, 3, 25, DateTimeOffset.UtcNow, Guid.NewGuid());

        Assert.Equal("report.published", report.SetFormTeacherComment("Changed")?.Code);
        Assert.Equal("report.published", report.SetPrincipalComment("Changed")?.Code);
        Assert.Equal("report.published", report.SetAttendance(60, 58)?.Code);
        Assert.Equal("Hardworking pupil.", report.FormTeacherComment);

        report.Unpublish();
        Assert.Equal(ReportStatus.Draft, report.Status);
        Assert.Null(report.Position);
        Assert.Null(report.SetPrincipalComment("Well done."));
    }

    [Theory]
    [InlineData(60, 58, null)]
    [InlineData(60, 60, null)]
    [InlineData(0, 0, null)]
    [InlineData(60, 61, "daysPresent")]
    [InlineData(60, -1, "daysPresent")]
    [InlineData(201, 10, "daysOpened")]
    public void Attendance_rules(int opened, int present, string? field)
    {
        var report = new TermReport();
        Assert.Equal(field, report.SetAttendance(opened, present)?.Field);
        if (field is null) Assert.Equal(opened - present, report.DaysAbsent);
    }

    [Fact]
    public void Blank_comments_are_stored_as_none()
    {
        var report = new TermReport { FormTeacherComment = "x" };
        Assert.Null(report.SetFormTeacherComment("   "));
        Assert.Null(report.FormTeacherComment);
    }
}

public sealed class ReportMathTests
{
    [Fact]
    public void Average_is_the_mean_of_subjects_taken_rounded_half_up()
    {
        Assert.Equal(70.00m, ReportMath.Average([69.99m, 70m, 70.005m])); // 69.998333… → 70.00
        Assert.Equal(66.67m, ReportMath.Average([60m, 70m, 70m]));
        Assert.Null(ReportMath.Average([]));
    }

    [Fact]
    public void Class_average_and_subject_stats()
    {
        Assert.Equal(55.5m, ReportMath.ClassAverage([60m, 51m]));
        Assert.Equal(0, ReportMath.ClassAverage([]));

        var stats = ReportMath.ForSubject([40m, 85.5m, 62m]);
        Assert.Equal((85.5m, 40m, 62.5m), (stats.Highest, stats.Lowest, stats.Average));
    }

    [Fact]
    public void Halves_round_away_from_zero() => Assert.Equal(62.13m, ReportMath.Round(62.125m));
}

public sealed class TermComparisonTests
{
    [Theory]
    [InlineData(72, 65, Trend.Improved)]
    [InlineData(64, 65, Trend.Declined)]
    [InlineData(65.5, 65, Trend.Steady)]
    [InlineData(64.2, 65, Trend.Steady)]
    public void Trend_ignores_movements_under_one_mark(decimal current, decimal previous, Trend expected) =>
        Assert.Equal(expected, ReportMath.TrendOf(current, previous));

    [Fact]
    public void Cumulative_average_needs_at_least_two_terms()
    {
        Assert.Null(ReportMath.Cumulative([70m]));
        Assert.Equal(72.33m, ReportMath.Cumulative([70m, 72m, 75m]));
    }
}
