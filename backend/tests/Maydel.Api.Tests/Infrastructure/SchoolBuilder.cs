using System.Net.Http.Json;
using Maydel.Application.Academics;
using Maydel.Application.Identity;
using Maydel.Application.StaffMembers;
using Maydel.Application.Students;
using Maydel.Domain.Common;

namespace Maydel.Api.Tests.Infrastructure;

/// <summary>A fresh class: 3 active students, two subjects (the first taught by <c>SubjectTeacher</c>), and a separate <c>FormTeacher</c>.</summary>
public sealed record School(
    Guid ClassRoomId, Guid SubjectId, Guid OtherSubjectId, Guid TermId, IReadOnlyList<Guid> Students,
    HttpClient SubjectTeacher, HttpClient FormTeacher);

public static class SchoolBuilder
{
    public static async Task<School> SetUpAsync(ApiFactory api)
    {
        var admin = await api.ClientAsync(Roles.Admin);
        var (subjectTeacher, subjectStaff) = await TeacherAsync(api);
        var (formTeacher, formStaff) = await TeacherAsync(api);

        var level = (await admin.GetFromJsonAsync<List<ClassLevelDto>>("/api/academics/class-levels", Json.Options))!.First();
        var room = (await (await admin.PostAsJsonAsync("/api/academics/classrooms",
            new { classLevelId = level.Id, arm = "G" + Guid.NewGuid().ToString("N")[..8], capacity = 30, formTeacherId = formStaff.Id }))
            .Content.ReadFromJsonAsync<ClassRoomDto>(Json.Options))!;
        var subjects = (await admin.GetFromJsonAsync<List<SubjectDto>>("/api/academics/subjects", Json.Options))!.Where(s => s.IsActive).Take(2).ToList();
        (await admin.PutAsJsonAsync($"/api/academics/classrooms/{room.Id}/subjects", new
        {
            subjects = new object[] { new { subjectId = subjects[0].Id, teacherId = subjectStaff.Id }, new { subjectId = subjects[1].Id, teacherId = (Guid?)null } },
        })).EnsureSuccessStatusCode();

        var students = new List<Guid>();
        foreach (var name in new[] { "Adaeze", "Bola", "Chidi" })
        {
            var r = await admin.PostAsJsonAsync("/api/students", new { firstName = name, lastName = "Test", dateOfBirth = "2018-03-10", classRoomId = room.Id, admittedOn = "2026-09-14" });
            students.Add((await r.Content.ReadFromJsonAsync<StudentDetail>(Json.Options))!.Id);
        }
        var term = (await admin.GetFromJsonAsync<TermDto>("/api/academics/terms/current", Json.Options))!;
        return new School(room.Id, subjects[0].Id, subjects[1].Id, term.Id, students, subjectTeacher, formTeacher);
    }

    /// <summary>A teacher with a staff record, signed in with a changed password.</summary>
    public static async Task<(HttpClient Client, StaffDetail Staff)> TeacherAsync(ApiFactory api)
    {
        var admin = await api.ClientAsync(Roles.Admin);
        var email = ApiFactory.UniqueEmail();
        var created = (await (await admin.PostAsJsonAsync("/api/staff", new { fullName = "Teacher " + email[..6], email, roles = new[] { "Teacher" } }))
            .Content.ReadFromJsonAsync<StaffCreated>(Json.Options))!;
        var first = await api.LoginAsync(email, created.Credentials!.TemporaryPassword);
        var changed = await api.CreateClient(first.AccessToken).PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = created.Credentials.TemporaryPassword, newPassword = "Brand#NewPass2026" });
        var session = (await changed.Content.ReadFromJsonAsync<AuthResponse>(Json.Options))!;
        return (api.CreateClient(session.AccessToken), created.Staff);
    }
}
