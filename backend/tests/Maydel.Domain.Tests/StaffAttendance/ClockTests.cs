using Maydel.Domain.StaffAttendance;

namespace Maydel.Domain.Tests.StaffAttendance;

public sealed class ClockTests
{
    private static readonly DateTimeOffset Morning = new(2026, 10, 26, 6, 40, 0, TimeSpan.Zero); // 07:40 in Abuja (Monday)

    [Fact]
    public void Codes_rotate_and_expire_within_a_minute_and_cannot_be_forged()
    {
        var s = new ClockSettings();
        var code = s.CodeFor(ClockSettings.WindowAt(Morning));
        Assert.True(s.Accepts(code, Morning));
        Assert.True(s.Accepts(code, Morning.AddSeconds(30))); // the next window still accepts the previous one
        Assert.False(s.Accepts(code, Morning.AddSeconds(65)));
        Assert.False(s.Accepts(code[..^1] + (code[^1] == '0' ? '1' : '0'), Morning));
        Assert.False(new ClockSettings().Accepts(code, Morning)); // another school's secret
        Assert.False(s.Accepts("garbage", Morning));
    }

    [Fact]
    public void First_scan_clocks_in_second_clocks_out_and_lateness_uses_the_grace_period()
    {
        var s = new ClockSettings { StartsAt = new TimeOnly(7, 30), GraceMinutes = 15 };
        var day = new StaffDay { Date = new DateOnly(2026, 10, 26) };
        Assert.Null(day.Scan(Morning, s, null, null, out var cameIn));
        Assert.True(cameIn);
        Assert.False(day.IsLate); // 07:40 is within 15 minutes of 07:30
        Assert.Equal("clock.too_soon", day.Scan(Morning.AddSeconds(20), s, null, null, out _)?.Code);
        Assert.Null(day.Scan(Morning.AddHours(8), s, null, null, out var cameInAgain));
        Assert.False(cameInAgain);
        Assert.Equal(TimeSpan.FromHours(8), day.Worked);
        Assert.Equal("clock.done", day.Scan(Morning.AddHours(9), s, null, null, out _)?.Code);

        var late = new StaffDay { Date = new DateOnly(2026, 10, 26) };
        late.Scan(Morning.AddMinutes(10), s, null, null, out _); // 07:50
        Assert.True(late.IsLate);
    }

    [Fact]
    public void Corrections_need_a_reason_and_stay_on_the_day()
    {
        var s = new ClockSettings();
        var day = new StaffDay { Date = new DateOnly(2026, 10, 26) };
        Assert.Equal("note", day.Correct(Morning, null, " ", s)?.Field);
        Assert.Equal("clockOutAt", day.Correct(Morning, Morning.AddMinutes(-5), "Forgot", s)?.Field);
        Assert.Equal("clockInAt", day.Correct(Morning.AddDays(1), null, "Forgot", s)?.Field);
        Assert.Null(day.Correct(Morning, Morning.AddHours(8), "Forgot to scan out", s));
        Assert.Equal((ClockMethod.Manual, ClockMethod.Manual), (day.InMethod, day.OutMethod!.Value));
    }

    [Fact]
    public void Distance_is_in_metres()
    {
        // About 111 m per 0.001° of latitude.
        Assert.InRange(ClockSettings.Distance(9.0, 7.4, 9.001, 7.4), 105, 117);
    }
}
