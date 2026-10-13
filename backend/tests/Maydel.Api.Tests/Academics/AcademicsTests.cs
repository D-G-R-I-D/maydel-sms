using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Academics;
using Maydel.Domain.Academics;
using Maydel.Domain.Assessment;
using Maydel.Domain.Common;
using Maydel.Domain.People;
using Maydel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Maydel.Api.Tests.Academics;

/// <summary>E2.1–E2.3, E4.2 — school setup over HTTP, including who may not change it.</summary>
[Collection(nameof(ApiCollection))]
public sealed class AcademicsTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    // ---------- Sessions & terms ----------

    [Fact]
    public async Task Admin_creates_a_session_with_terms_and_switches_the_current_term_atomically()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var year = Random.Shared.Next(2200, 9000);

        var create = await admin.PostAsJsonAsync("/api/academics/sessions",
            new { name = $"{year}/{year + 1}", startsOn = $"{year}-09-14", endsOn = $"{year + 1}-07-23" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var session = (await create.Content.ReadFromJsonAsync<SessionDto>(Json.Options))!;
        Assert.Equal($"/api/academics/sessions/{session.Id}", create.Headers.Location?.OriginalString);

        var termResponse = await admin.PostAsJsonAsync($"/api/academics/sessions/{session.Id}/terms", new
        {
            number = "First", startsOn = $"{year}-09-14", endsOn = $"{year}-12-11",
            midTermStartsOn = $"{year}-10-26", midTermEndsOn = $"{year}-10-30",
        });
        Assert.Equal(HttpStatusCode.Created, termResponse.StatusCode);
        var term = (await termResponse.Content.ReadFromJsonAsync<TermDto>(Json.Options))!;
        Assert.Equal($"First Term {year}/{year + 1}", term.Name);

        (await admin.PostAsync($"/api/academics/terms/{term.Id}/make-current", null)).EnsureSuccessStatusCode();

        await Api.WithServicesAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            Assert.Equal(term.Id, Assert.Single(await db.Terms.Where(t => t.IsCurrent).ToListAsync()).Id);
            Assert.Equal(session.Id, Assert.Single(await db.Sessions.Where(s => s.IsCurrent).ToListAsync()).Id);
        });

        // Anyone signed in can read the current term (portal headers show it).
        var parent = await Api.ClientAsync(Roles.Parent);
        var current = await parent.GetFromJsonAsync<TermDto>("/api/academics/terms/current", Json.Options);
        Assert.Equal(term.Id, current!.Id);

        await RestoreSeededCurrentTermAsync(admin);
    }

    [Fact]
    public async Task Switching_the_current_term_is_audited()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var (_, termId) = await CreateSessionWithFirstTermAsync(admin);

        (await admin.PostAsync($"/api/academics/terms/{termId}/make-current", null)).EnsureSuccessStatusCode();

        await Api.WithServicesAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            Assert.True(await db.AuditLogs.AnyAsync(l => l.EntityType == nameof(Term) && l.EntityId == termId.ToString() && l.Action == "Modified"));
        });
        await RestoreSeededCurrentTermAsync(admin);
    }

    [Fact]
    public async Task Session_and_term_rules_come_back_as_field_errors()
    {
        var admin = await Api.ClientAsync(Roles.Admin);

        var badName = await admin.PostAsJsonAsync("/api/academics/sessions", new { name = "2026-2027", startsOn = "2026-09-14", endsOn = "2027-07-23" });
        Assert.True((await badName.AssertProblemAsync(HttpStatusCode.BadRequest, "validation")).GetProperty("errors").TryGetProperty("name", out _));

        var (sessionId, _) = await CreateSessionWithFirstTermAsync(admin);
        var duplicate = await admin.PostAsJsonAsync($"/api/academics/sessions/{sessionId}/terms",
            new { number = "First", startsOn = "2027-01-11", endsOn = "2027-03-26" });
        await duplicate.AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
    }

    [Fact]
    public async Task Duplicate_session_name_is_a_conflict()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var seeded = (await admin.GetFromJsonAsync<List<SessionDto>>("/api/academics/sessions", Json.Options))!.First();

        var response = await admin.PostAsJsonAsync("/api/academics/sessions",
            new { name = seeded.Name, startsOn = seeded.StartsOn, endsOn = seeded.EndsOn });

        await response.AssertProblemAsync(HttpStatusCode.Conflict, "session.duplicate");
    }

    // ---------- Classes ----------

    [Fact]
    public async Task Admin_adds_an_arm_with_a_form_teacher_and_it_lists_with_counts()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var level = await AnyLevelAsync(admin);
        var teacher = await CreateStaffAsync();
        var arm = "T" + Guid.NewGuid().ToString("N")[..6];

        var created = await admin.PostAsJsonAsync("/api/academics/classrooms", new { classLevelId = level.Id, arm, capacity = 25, formTeacherId = teacher.Id });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var room = (await created.Content.ReadFromJsonAsync<ClassRoomDto>(Json.Options))!;
        Assert.Equal($"{level.Name} {arm}", room.Name);
        Assert.Equal(teacher.FullName, room.FormTeacher);
        Assert.Equal(0, room.StudentCount);

        var teacherClient = await Api.ClientAsync(Roles.Teacher); // staff can read the list
        var rooms = (await teacherClient.GetFromJsonAsync<List<ClassRoomDto>>("/api/academics/classrooms", Json.Options))!;
        Assert.Contains(rooms, r => r.Id == room.Id);

        var duplicate = await admin.PostAsJsonAsync("/api/academics/classrooms", new { classLevelId = level.Id, arm = arm.ToLowerInvariant(), capacity = 25 });
        await duplicate.AssertProblemAsync(HttpStatusCode.Conflict, "classroom.duplicate");
    }

    [Fact]
    public async Task Capacity_cannot_drop_below_enrolled_students()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var room = await CreateClassRoomAsync(admin);
        await Api.WithServicesAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            for (var i = 0; i < 3; i++)
                db.Students.Add(new Student { AdmissionNumber = $"T/{Guid.NewGuid():N}"[..20], FirstName = "Test", LastName = $"Pupil{i}", ClassRoomId = room.Id });
            await db.SaveChangesAsync();
        });

        var response = await admin.PutAsJsonAsync($"/api/academics/classrooms/{room.Id}", new { arm = room.Arm, capacity = 2, formTeacherId = (Guid?)null });

        var errors = (await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation")).GetProperty("errors");
        Assert.True(errors.TryGetProperty("capacity", out _));
    }

    [Fact]
    public async Task Admin_sets_class_subjects_and_teachers_in_one_call()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var room = await CreateClassRoomAsync(admin);
        var subjects = (await admin.GetFromJsonAsync<List<SubjectDto>>("/api/academics/subjects", Json.Options))!.Where(s => s.IsActive).Take(3).ToList();
        var teacher = await CreateStaffAsync();

        var set = await admin.PutAsJsonAsync($"/api/academics/classrooms/{room.Id}/subjects", new
        {
            subjects = new object[]
            {
                new { subjectId = subjects[0].Id, teacherId = teacher.Id },
                new { subjectId = subjects[1].Id, teacherId = (Guid?)null },
            },
        });
        set.EnsureSuccessStatusCode();
        var offered = (await set.Content.ReadFromJsonAsync<List<ClassSubjectDto>>(Json.Options))!;
        Assert.Equal(2, offered.Count);
        Assert.Equal(teacher.FullName, offered.Single(o => o.SubjectId == subjects[0].Id).Teacher);

        // Replace: drop one, add one, reassign.
        set = await admin.PutAsJsonAsync($"/api/academics/classrooms/{room.Id}/subjects", new
        {
            subjects = new object[]
            {
                new { subjectId = subjects[0].Id, teacherId = (Guid?)null },
                new { subjectId = subjects[2].Id, teacherId = teacher.Id },
            },
        });
        set.EnsureSuccessStatusCode();
        offered = (await set.Content.ReadFromJsonAsync<List<ClassSubjectDto>>(Json.Options))!;
        Assert.Equal(new[] { subjects[0].Id, subjects[2].Id }.Order(), offered.Select(o => o.SubjectId).Order());
        Assert.Null(offered.Single(o => o.SubjectId == subjects[0].Id).TeacherId);
    }

    [Fact]
    public async Task A_subject_with_scores_cannot_be_removed_from_the_class()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var room = await CreateClassRoomAsync(admin);
        var subject = (await admin.GetFromJsonAsync<List<SubjectDto>>("/api/academics/subjects", Json.Options))!.First(s => s.IsActive);
        (await admin.PutAsJsonAsync($"/api/academics/classrooms/{room.Id}/subjects",
            new { subjects = new[] { new { subjectId = subject.Id, teacherId = (Guid?)null } } })).EnsureSuccessStatusCode();

        await Api.WithServicesAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var student = new Student { AdmissionNumber = $"S/{Guid.NewGuid():N}"[..20], FirstName = "Scored", LastName = "Pupil", ClassRoomId = room.Id };
            db.Students.Add(student);
            var termId = await db.Terms.Select(t => t.Id).FirstAsync();
            db.SubjectScores.Add(new SubjectScore { StudentId = student.Id, TermId = termId, SubjectId = subject.Id, ClassRoomId = room.Id });
            await db.SaveChangesAsync();
        });

        var remove = await admin.PutAsJsonAsync($"/api/academics/classrooms/{room.Id}/subjects", new { subjects = Array.Empty<object>() });

        var body = await remove.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "class_subjects.has_scores");
        Assert.Contains(subject.Name, body.GetProperty("detail").GetString());
    }

    // ---------- Subjects ----------

    [Fact]
    public async Task Admin_manages_the_subject_catalogue_with_unique_codes()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var tag = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        var created = await admin.PostAsJsonAsync("/api/academics/subjects", new { name = $"Robotics {tag}", code = $" r{tag.ToLowerInvariant()} " });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var subject = (await created.Content.ReadFromJsonAsync<SubjectDto>(Json.Options))!;
        Assert.Equal($"R{tag}", subject.Code);

        var clash = await admin.PostAsJsonAsync("/api/academics/subjects", new { name = $"Other {tag}", code = $"R{tag}" });
        await clash.AssertProblemAsync(HttpStatusCode.Conflict, "subject.duplicate");

        var deactivated = await admin.PutAsJsonAsync($"/api/academics/subjects/{subject.Id}", new { name = subject.Name, code = subject.Code, isActive = false });
        Assert.False((await deactivated.Content.ReadFromJsonAsync<SubjectDto>(Json.Options))!.IsActive);

        // Inactive subjects can't be newly offered.
        var room = await CreateClassRoomAsync(admin);
        var offer = await admin.PutAsJsonAsync($"/api/academics/classrooms/{room.Id}/subjects",
            new { subjects = new[] { new { subjectId = subject.Id, teacherId = (Guid?)null } } });
        await offer.AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
    }

    [Fact]
    public async Task Missing_fields_are_field_errors_not_server_errors()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var level = await AnyLevelAsync(admin);
        var room = await CreateClassRoomAsync(admin);

        await (await admin.PostAsJsonAsync("/api/academics/subjects", new { })).AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        await (await admin.PostAsJsonAsync("/api/academics/classrooms", new { classLevelId = level.Id, capacity = 10 })).AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        await (await admin.PutAsJsonAsync($"/api/academics/classrooms/{room.Id}/subjects", new { })).AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        await (await admin.PostAsJsonAsync("/api/academics/sessions", new { })).AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
    }

    // ---------- Authorization ----------

    public static TheoryData<string> NonAdmins => [Roles.Teacher, Roles.Bursar, Roles.Parent, Roles.Student];

    [Theory]
    [MemberData(nameof(NonAdmins))]
    public async Task Only_admins_can_change_school_setup(string role)
    {
        var client = await Api.ClientAsync(role);
        var id = Guid.NewGuid();

        await (await client.PostAsJsonAsync("/api/academics/sessions", new { name = "2990/2991", startsOn = "2990-09-01", endsOn = "2991-07-01" })).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await client.PostAsync($"/api/academics/terms/{id}/make-current", null)).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await client.PostAsJsonAsync("/api/academics/classrooms", new { classLevelId = id, arm = "Z", capacity = 10 })).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await client.PutAsJsonAsync($"/api/academics/classrooms/{id}/subjects", new { subjects = Array.Empty<object>() })).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await client.PostAsJsonAsync("/api/academics/subjects", new { name = "X", code = "X" })).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(Roles.Parent)]
    [InlineData(Roles.Student)]
    public async Task Parents_and_students_cannot_read_school_setup_lists(string role)
    {
        var client = await Api.ClientAsync(role);

        foreach (var path in new[] { "/api/academics/sessions", "/api/academics/classrooms", "/api/academics/subjects", "/api/academics/class-levels" })
            await (await client.GetAsync(path)).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    // ---------- Helpers ----------

    private static async Task<ClassLevelDto> AnyLevelAsync(HttpClient admin) =>
        (await admin.GetFromJsonAsync<List<ClassLevelDto>>("/api/academics/class-levels", Json.Options))!.First();

    private async Task<ClassRoomDto> CreateClassRoomAsync(HttpClient admin)
    {
        var level = await AnyLevelAsync(admin);
        var response = await admin.PostAsJsonAsync("/api/academics/classrooms",
            new { classLevelId = level.Id, arm = "T" + Guid.NewGuid().ToString("N")[..8], capacity = 30 });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ClassRoomDto>(Json.Options))!;
    }

    private async Task<Staff> CreateStaffAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var staff = new Staff { StaffNumber = $"TST-{tag}", FullName = $"Teacher {tag}", Email = $"{tag}@staff.test" };
        await Api.WithServicesAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            db.Staff.Add(staff);
            await db.SaveChangesAsync();
        });
        return staff;
    }

    private static async Task<(Guid SessionId, Guid TermId)> CreateSessionWithFirstTermAsync(HttpClient admin)
    {
        var year = Random.Shared.Next(2200, 9000);
        var s = await admin.PostAsJsonAsync("/api/academics/sessions", new { name = $"{year}/{year + 1}", startsOn = $"{year}-09-14", endsOn = $"{year + 1}-07-23" });
        s.EnsureSuccessStatusCode();
        var session = (await s.Content.ReadFromJsonAsync<SessionDto>(Json.Options))!;
        var t = await admin.PostAsJsonAsync($"/api/academics/sessions/{session.Id}/terms", new { number = "First", startsOn = $"{year}-09-14", endsOn = $"{year}-12-11" });
        t.EnsureSuccessStatusCode();
        return (session.Id, (await t.Content.ReadFromJsonAsync<TermDto>(Json.Options))!.Id);
    }

    /// <summary>Other tests assume the seeded 2026/2027 First term is current; put it back.</summary>
    private static async Task RestoreSeededCurrentTermAsync(HttpClient admin)
    {
        var seeded = (await admin.GetFromJsonAsync<List<SessionDto>>("/api/academics/sessions", Json.Options))!.Single(s => s.Name == "2026/2027");
        (await admin.PostAsync($"/api/academics/terms/{seeded.Terms.Single(t => t.Number == TermNumber.First).Id}/make-current", null)).EnsureSuccessStatusCode();
    }
}
