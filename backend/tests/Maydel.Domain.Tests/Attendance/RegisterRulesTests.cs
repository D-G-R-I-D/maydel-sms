using Maydel.Domain.Academics;
using Maydel.Domain.Attendance;

namespace Maydel.Domain.Tests.Attendance;

public sealed class RegisterRulesTests
{
    private static readonly Term Term = new()
    {
        StartsOn = new(2026, 9, 14), EndsOn = new(2026, 12, 11), MidTermStartsOn = new(2026, 10, 26), MidTermEndsOn = new(2026, 10, 30),
    };

    [Theory]
    [InlineData("2026-10-15", null)]                       // Thursday
    [InlineData("2026-10-17", "attendance.weekend")]       // Saturday
    [InlineData("2026-10-18", "attendance.weekend")]       // Sunday
    [InlineData("2026-10-27", "attendance.mid_term")]
    public void Registers_are_for_weekdays_outside_mid_term(string date, string? code) =>
        Assert.Equal(code, RegisterRules.CheckSchoolDay(DateOnly.Parse(date), Term)?.Code);

    [Fact]
    public void A_date_outside_every_term_has_no_register() =>
        Assert.Equal("attendance.not_in_term", RegisterRules.CheckSchoolDay(new DateOnly(2026, 12, 20), null)?.Code);

    [Theory]
    [InlineData("2026-10-15", false, null)]
    [InlineData("2026-10-14", false, "attendance.past_day")]
    [InlineData("2026-10-14", true, null)]
    [InlineData("2026-10-16", true, "attendance.future")]
    public void Form_teachers_edit_today_admins_correct_earlier_days(string date, bool admin, string? code) =>
        Assert.Equal(code, RegisterRules.CheckEditable(DateOnly.Parse(date), new DateOnly(2026, 10, 15), admin)?.Code);

    [Fact]
    public void Today_is_counted_in_west_africa_time() =>
        // 23:30 UTC on the 14th is already 00:30 on the 15th in Lagos.
        Assert.Equal(new DateOnly(2026, 10, 15), RegisterRules.TodayAt(new DateTimeOffset(2026, 10, 14, 23, 30, 0, TimeSpan.Zero)));

    [Fact]
    public void Late_counts_as_present_and_blank_notes_are_dropped()
    {
        var record = new AttendanceRecord();
        record.Set(AttendanceStatus.Late, "  ", null);
        Assert.True(record.CountsAsPresent);
        Assert.Null(record.Note);
        record.Set(AttendanceStatus.Excused, " Hospital visit ", null);
        Assert.False(record.CountsAsPresent);
        Assert.Equal("Hospital visit", record.Note);
    }
}
