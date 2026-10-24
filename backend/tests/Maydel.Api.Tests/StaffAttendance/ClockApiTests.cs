using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Admissions;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.StaffAttendance;
using Maydel.Domain.Common;

namespace Maydel.Api.Tests.StaffAttendance;

/// <summary>Staff clock in and out by scanning the kiosk's rotating code; the admin sees the board, report and can correct.</summary>
[Collection(nameof(ApiCollection))]
public sealed class ClockApiTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task Teachers_scan_the_kiosk_code_to_clock_in_and_out_and_the_admin_sees_it()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var (teacher, staff) = await SchoolBuilder.TeacherAsync(Api);
        var original = Api.Clock.Now;
        try
        {
            // Monday 26 October 2026, 07:40 in Abuja.
            Api.Clock.Now = new DateTimeOffset(2026, 10, 26, 6, 40, 0, TimeSpan.Zero);
            Api.Clock.Today = new DateOnly(2026, 10, 26);
            (await admin.PutAsJsonAsync("/api/clock/settings", new { startsAt = "07:30", endsAt = "16:00", graceMinutes = 15, requireLocation = false, radiusMeters = 150 }))
                .EnsureSuccessStatusCode();
            var replaced = await AdmissionsKit.ReadAsync<KioskLink>(await admin.PostAsync("/api/clock/kiosk-link", null));
            var link = await AdmissionsKit.ReadAsync<KioskLink>(await admin.PostAsync("/api/clock/kiosk-link", null));
            // The admin can copy the current link again; only the newest one works.
            Assert.Equal(link.Token, (await AdmissionsKit.ReadAsync<KioskLink>(await admin.GetAsync("/api/clock/kiosk-link"))).Token);
            await (await teacher.GetAsync("/api/clock/kiosk-link")).AssertProblemAsync(HttpStatusCode.Forbidden);

            var kiosk = Api.CreateClient();
            await (await kiosk.GetAsync($"/api/public/clock/kiosk?token={replaced.Token}")).AssertProblemAsync(HttpStatusCode.Unauthorized, "clock.kiosk");
            await (await kiosk.GetAsync("/api/public/clock/kiosk?token=wrong")).AssertProblemAsync(HttpStatusCode.Unauthorized);
            var code = await AdmissionsKit.ReadAsync<KioskCode>(await kiosk.GetAsync($"/api/public/clock/kiosk?token={link.Token}"));
            Assert.Contains("/clock?c=", code.Url);

            await (await teacher.PostAsJsonAsync("/api/clock", new { code = "123.ABCDEF012345" })).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "clock.code");
            var clockedIn = await AdmissionsKit.ReadAsync<ClockResult>(await teacher.PostAsJsonAsync("/api/clock", new { code = code.Code }));
            Assert.Equal((true, DayState.In, false), (clockedIn.ClockedIn, clockedIn.Day.State, clockedIn.Day.IsLate));

            // A photo of the code is useless a minute later.
            Api.Clock.Now = Api.Clock.Now.AddMinutes(2);
            await (await teacher.PostAsJsonAsync("/api/clock", new { code = code.Code })).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "clock.code");

            Api.Clock.Now = new DateTimeOffset(2026, 10, 26, 15, 5, 0, TimeSpan.Zero); // 16:05
            var later = await AdmissionsKit.ReadAsync<KioskCode>(await kiosk.GetAsync($"/api/public/clock/kiosk?token={link.Token}"));
            var clockedOut = await AdmissionsKit.ReadAsync<ClockResult>(await teacher.PostAsJsonAsync("/api/clock", new { code = later.Code }));
            Assert.Equal((false, DayState.Out), (clockedOut.ClockedIn, clockedOut.Day.State));
            Assert.Equal(8.42, clockedOut.Day.HoursWorked);

            var board = await AdmissionsKit.ReadAsync<DayBoard>(await admin.GetAsync("/api/clock/board?date=2026-10-26"));
            Assert.Equal(DayState.Out, board.Staff.Single(r => r.StaffId == staff.Id).State);
            Assert.True(board.Absent > 0); // other staff didn't come

            var fixedDay = await AdmissionsKit.ReadAsync<StaffDayDto>(await admin.PutAsJsonAsync($"/api/clock/days/{staff.Id}/2026-10-27",
                new { clockInAt = "2026-10-27T08:05:00+01:00", clockOutAt = "2026-10-27T15:30:00+01:00", note = "Forgot phone; signed the book" }));
            Assert.True(fixedDay.IsLate);
            var report = await AdmissionsKit.ReadAsync<AttendanceReport>(await admin.GetAsync("/api/clock/report?from=2026-10-26&to=2026-10-30"));
            var row = report.Staff.Single(r => r.StaffId == staff.Id);
            Assert.Equal((5, 2, 1, 3), (report.SchoolDays, row.DaysPresent, row.DaysLate, row.DaysAbsent));
            Assert.Contains("Hours worked", await admin.GetStringAsync("/api/clock/report.csv?from=2026-10-26&to=2026-10-30"));

            await (await teacher.GetAsync("/api/clock/board")).AssertProblemAsync(HttpStatusCode.Forbidden);
            await (await (await Api.ClientAsync(Roles.Parent)).PostAsJsonAsync("/api/clock", new { code = later.Code })).AssertProblemAsync(HttpStatusCode.Forbidden);
            Assert.Equal(2, (await AdmissionsKit.ReadAsync<List<StaffDayDto>>(await teacher.GetAsync("/api/clock/me?from=2026-10-01&to=2026-10-31"))).Count);
        }
        finally
        {
            Api.Clock.Now = original;
            Api.Clock.Today = FakeSchoolClock.Default;
        }
    }

    [Fact]
    public async Task With_location_required_scans_from_far_away_are_refused()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var (teacher, _) = await SchoolBuilder.TeacherAsync(Api);
        var original = Api.Clock.Now;
        try
        {
            Api.Clock.Now = new DateTimeOffset(2026, 10, 28, 6, 30, 0, TimeSpan.Zero); // Wednesday
            (await admin.PutAsJsonAsync("/api/clock/settings", new { startsAt = "07:30", endsAt = "16:00", graceMinutes = 15, requireLocation = true, latitude = 9.0, longitude = 7.4, radiusMeters = 150 }))
                .EnsureSuccessStatusCode();
            var link = await AdmissionsKit.ReadAsync<KioskLink>(await admin.PostAsync("/api/clock/kiosk-link", null));
            var code = (await AdmissionsKit.ReadAsync<KioskCode>(await Api.CreateClient().GetAsync($"/api/public/clock/kiosk?token={link.Token}"))).Code;
            await (await teacher.PostAsJsonAsync("/api/clock", new { code })).AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
            await (await teacher.PostAsJsonAsync("/api/clock", new { code, latitude = 9.05, longitude = 7.4 })).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "clock.too_far");
            (await teacher.PostAsJsonAsync("/api/clock", new { code, latitude = 9.0005, longitude = 7.4 })).EnsureSuccessStatusCode();
        }
        finally
        {
            Api.Clock.Now = original;
            (await admin.PutAsJsonAsync("/api/clock/settings", new { startsAt = "07:30", endsAt = "16:00", graceMinutes = 15, requireLocation = false, radiusMeters = 150 }))
                .EnsureSuccessStatusCode();
        }
    }
}
