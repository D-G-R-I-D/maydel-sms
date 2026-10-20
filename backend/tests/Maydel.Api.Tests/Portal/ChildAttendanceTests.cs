using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Admissions;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Identity;
using Maydel.Application.Portal.GetChildAttendance;
using Maydel.Domain.Attendance;
using Maydel.Domain.Common;
using Maydel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Maydel.Api.Tests.Portal;

/// <summary>E9.2 in the portal: a child's register marks and term totals, visible only to the family (and the student).</summary>
[Collection(nameof(ApiCollection))]
public sealed class ChildAttendanceTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task A_student_sees_their_own_register_marks_for_the_term_and_no_one_else_s()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        await Api.WithServicesAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var term = db.Terms.Single(t => t.Id == s.TermId);
            var day = term.StartsOn;
            foreach (var (offset, status) in new[] { (0, AttendanceStatus.Present), (1, AttendanceStatus.Late), (2, AttendanceStatus.Absent), (3, AttendanceStatus.Present) })
                db.Attendance.Add(new AttendanceRecord { StudentId = s.Students[0], ClassRoomId = s.ClassRoomId, Date = day.AddDays(offset), Status = status });
            await db.SaveChangesAsync();
        });

        var student = await StudentClientAsync(s.Students[0]);
        var mine = await AdmissionsKit.ReadAsync<ChildAttendance>(await student.GetAsync($"/api/portal/children/{s.Students[0]}/attendance?termId={s.TermId}"));
        Assert.Equal((4, 3, 1, 1, 75m), (mine.DaysRecorded, mine.Present, mine.Absent, mine.Late, mine.Percent!.Value));
        Assert.Equal(AttendanceStatus.Present, mine.Days[0].Status); // newest first

        await (await student.GetAsync($"/api/portal/children/{s.Students[1]}/attendance")).AssertProblemAsync(HttpStatusCode.NotFound, "portal.student_not_found");
        await (await (await Api.ClientAsync(Roles.Parent)).GetAsync($"/api/portal/children/{s.Students[0]}/attendance")).AssertProblemAsync(HttpStatusCode.NotFound);
    }

    private async Task<HttpClient> StudentClientAsync(Guid studentId)
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var issued = (await (await admin.PostAsync($"/api/students/{studentId}/login", null)).Content.ReadFromJsonAsync<IssuedCredentials>(Json.Options))!;
        var first = await Api.LoginAsync(issued.UserName, issued.TemporaryPassword);
        var change = await Api.CreateClient(first.AccessToken).PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = issued.TemporaryPassword, newPassword = "Student#NewPass2026" });
        change.EnsureSuccessStatusCode();
        return Api.CreateClient((await change.Content.ReadFromJsonAsync<AuthResponse>(Json.Options))!.AccessToken);
    }
}
