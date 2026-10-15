using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Assessment;
using Maydel.Application.Attendance;
using Maydel.Application.Reports;
using Maydel.Domain.Attendance;
using Maydel.Domain.Common;

namespace Maydel.Api.Tests.Attendance;

/// <summary>E9.1 daily register and E9.2 term totals feeding report cards.</summary>
[Collection(nameof(ApiCollection))]
public sealed class RegisterTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;
    private static readonly DateOnly Today = FakeSchoolClock.Default;        // Thursday 15 Oct 2026, first term
    private static readonly DateOnly Monday = new(2026, 10, 12);

    [Fact]
    public async Task Form_teacher_takes_todays_register_in_one_go_with_everyone_else_present()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);

        var empty = await RegisterAsync(s.FormTeacher, s, Today);
        Assert.Equal((false, true, 3), (empty.Taken, empty.CanEdit, empty.Present));

        var saved = await SaveAsync(s.FormTeacher, s, Today, (s.Students[1], AttendanceStatus.Absent, " Sick "), (s.Students[2], AttendanceStatus.Late, null));

        Assert.True(saved.Taken);
        Assert.NotNull(saved.TakenAt);
        Assert.Null(empty.TakenAt);
        Assert.Equal((1, 1, 1, 0), (saved.Present, saved.Absent, saved.Late, saved.Excused));
        var bola = saved.Entries.Single(e => e.StudentId == s.Students[1]);
        Assert.Equal((AttendanceStatus.Absent, "Sick", true), (bola.Status, bola.Note, bola.Recorded));

        // Saving again updates in place: still one record per student per day.
        var again = await SaveAsync(s.FormTeacher, s, Today);
        Assert.Equal(3, again.Present);
    }

    [Fact]
    public async Task Only_admins_correct_earlier_days_and_nobody_records_the_future()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);

        Assert.False((await RegisterAsync(s.FormTeacher, s, Monday)).CanEdit);
        await (await PutAsync(s.FormTeacher, s, Monday)).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "attendance.past_day");
        Assert.Equal(HttpStatusCode.OK, (await PutAsync(admin, s, Monday)).StatusCode);
        await (await PutAsync(admin, s, Today.AddDays(1))).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "attendance.future");
    }

    [Theory]
    [InlineData("2026-10-10", "attendance.weekend")]
    [InlineData("2026-12-20", "attendance.not_in_term")]
    public async Task There_is_no_register_on_non_school_days(string date, string code)
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        Api.Clock.Today = new DateOnly(2027, 1, 1); // so the date isn't "in the future"
        try
        {
            var register = await RegisterAsync(admin, s, DateOnly.Parse(date));
            Assert.NotNull(register.Problem);
            Assert.False(register.CanEdit);
            await (await PutAsync(admin, s, DateOnly.Parse(date))).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, code);
        }
        finally
        {
            Api.Clock.Today = FakeSchoolClock.Default;
        }
    }

    [Fact]
    public async Task Subject_teachers_can_view_but_only_the_form_teacher_or_admin_takes_it()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var (outsider, _) = await SchoolBuilder.TeacherAsync(Api);

        Assert.False((await RegisterAsync(s.SubjectTeacher, s, Today)).CanEdit);
        await (await PutAsync(s.SubjectTeacher, s, Today)).AssertProblemAsync(HttpStatusCode.Forbidden, "attendance.not_form_teacher");
        await (await outsider.GetAsync($"/api/attendance/register?classRoomId={s.ClassRoomId}&date={Today:yyyy-MM-dd}")).AssertProblemAsync(HttpStatusCode.Forbidden, "attendance.not_allowed");
        await (await (await Api.ClientAsync(Roles.Parent)).GetAsync($"/api/attendance/register?classRoomId={s.ClassRoomId}")).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Students_from_other_classes_are_refused()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var response = await s.FormTeacher.PutAsJsonAsync("/api/attendance/register", new
        {
            classRoomId = s.ClassRoomId, date = Today, entries = new[] { new { studentId = Guid.NewGuid(), status = "Absent" } },
        });
        var errors = (await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation")).GetProperty("errors");
        Assert.True(errors.TryGetProperty("entries[0].studentId", out _));
    }

    [Fact]
    public async Task Register_totals_fill_the_report_card_and_publishing_freezes_them()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        await SaveAsync(admin, s, Monday, (s.Students[0], AttendanceStatus.Absent, null));
        await SaveAsync(admin, s, new DateOnly(2026, 10, 13), (s.Students[0], AttendanceStatus.Late, null));
        await SaveAsync(s.FormTeacher, s, Today, (s.Students[0], AttendanceStatus.Excused, "Clinic"));

        var summary = (await admin.GetFromJsonAsync<ClassAttendanceSummary>($"/api/attendance/summary?classRoomId={s.ClassRoomId}&termId={s.TermId}", Json.Options))!;
        Assert.Equal(3, summary.DaysOpened);
        var adaeze = summary.Rows.Single(r => r.StudentId == s.Students[0]);
        Assert.Equal((1, 1, 1, 1, (decimal?)33.33m), (adaeze.Present, adaeze.Absent, adaeze.Late, adaeze.Excused, adaeze.Percentage));

        // No attendance typed in: the report shows the register's totals, and publishing needs nothing else.
        await ScoreAsync(admin, s);
        var preview = await CardAsync(admin, s);
        Assert.Equal((3, 1, 2), (preview.DaysOpened!.Value, preview.DaysPresent!.Value, preview.DaysAbsent!.Value));
        (await admin.PostAsJsonAsync("/api/reports/publish", new { classRoomId = s.ClassRoomId, termId = s.TermId })).EnsureSuccessStatusCode();
        Assert.Equal((3, 1), ((await CardAsync(admin, s)).DaysOpened!.Value, (await CardAsync(admin, s)).DaysPresent!.Value));

        // Published: the register can't contradict the frozen report any more.
        await (await PutAsync(admin, s, Monday)).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "attendance.report_published");
        var locked = await RegisterAsync(admin, s, Monday);
        Assert.True(locked.Entries.Single(e => e.StudentId == s.Students[0]).Locked);
    }

    // ---------- helpers ----------

    private static async Task<ClassRegister> RegisterAsync(HttpClient client, School s, DateOnly date)
    {
        var response = await client.GetAsync($"/api/attendance/register?classRoomId={s.ClassRoomId}&date={date:yyyy-MM-dd}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ClassRegister>(Json.Options))!;
    }

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, School s, DateOnly date, params (Guid Student, AttendanceStatus Status, string? Note)[] marks) =>
        client.PutAsJsonAsync("/api/attendance/register", new
        {
            classRoomId = s.ClassRoomId, date,
            entries = marks.Select(m => new { studentId = m.Student, status = m.Status.ToString(), note = m.Note }),
        });

    private static async Task<ClassRegister> SaveAsync(HttpClient client, School s, DateOnly date, params (Guid Student, AttendanceStatus Status, string? Note)[] marks)
    {
        var response = await PutAsync(client, s, date, marks);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ClassRegister>(Json.Options))!;
    }

    private static async Task ScoreAsync(HttpClient admin, School s)
    {
        var settings = (await admin.GetFromJsonAsync<AssessmentSettings>("/api/assessment/settings", Json.Options))!;
        var cat = settings.Components.Single(c => c.Name == "CAT" && c.IsActive).Id;
        (await admin.PutAsJsonAsync("/api/assessment/gradesheet", new
        {
            classRoomId = s.ClassRoomId, subjectId = s.SubjectId, termId = s.TermId,
            entries = new[] { new { studentId = s.Students[0], scores = new[] { new { componentId = cat, score = (decimal?)25 } } } },
        })).EnsureSuccessStatusCode();
    }

    private static async Task<ReportCard> CardAsync(HttpClient client, School s)
    {
        var response = await client.GetAsync($"/api/reports/{s.Students[0]}/{s.TermId}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReportCard>(Json.Options))!;
    }
}
