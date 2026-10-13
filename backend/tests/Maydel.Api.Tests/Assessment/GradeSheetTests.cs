using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Academics;
using Maydel.Application.Assessment;
using Maydel.Application.Identity;
using Maydel.Application.StaffMembers;
using Maydel.Application.Students;
using Maydel.Domain.Assessment;
using Maydel.Domain.Common;
using Maydel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Maydel.Api.Tests.Assessment;

/// <summary>E2.4 settings, E5.1 score entry, E5.2 publish lock — with the teacher ↔ class/subject row rule.</summary>
[Collection(nameof(ApiCollection))]
public sealed class GradeSheetTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    // ---------- E5.1 entering scores ----------

    [Fact]
    public async Task Assigned_teacher_enters_scores_and_gets_totals_and_grades_back()
    {
        var school = await SetUpAsync();
        var sheet = await GetSheetAsync(school.SubjectTeacher, school);
        Assert.Equal(3, sheet.Rows.Count);
        Assert.Equal(100m, sheet.Components.Sum(c => c.MaxScore));
        Assert.All(sheet.Rows, r => Assert.Null(r.Total));

        var cat = sheet.Components.Single(c => c.Name == "CAT");
        var exams = sheet.Components.Single(c => c.Name == "Exams");
        var saved = await SaveAsync(school.SubjectTeacher, school, new
        {
            studentId = school.Students[0],
            scores = new[] { new { componentId = cat.Id, score = (decimal?)25 }, new { componentId = exams.Id, score = (decimal?)38.5m } },
            comment = "Good effort",
        });

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var row = (await saved.Content.ReadFromJsonAsync<GradeSheet>(Json.Options))!.Rows.Single(r => r.StudentId == school.Students[0]);
        Assert.Equal(63.5m, row.Total);
        Assert.Equal("B", row.Grade);
        Assert.Equal("Very Good", row.Remark);
        Assert.Equal("Good effort", row.Comment);
        Assert.Equal(38.5m, row.Scores.Single(s => s.ComponentId == exams.Id).Score);
    }

    [Fact]
    public async Task Saving_some_rows_leaves_the_others_alone_and_an_empty_row_clears_it()
    {
        var school = await SetUpAsync();
        var cat = (await GetSheetAsync(school.SubjectTeacher, school)).Components.Single(c => c.Name == "CAT");
        object Row(Guid id, decimal? score) => new { studentId = id, scores = new[] { new { componentId = cat.Id, score } }, comment = (string?)null };

        (await SaveAsync(school.SubjectTeacher, school, Row(school.Students[0], 20), Row(school.Students[1], 21))).EnsureSuccessStatusCode();
        (await SaveAsync(school.SubjectTeacher, school, Row(school.Students[1], 22))).EnsureSuccessStatusCode();
        var sheet = await GetSheetAsync(school.SubjectTeacher, school);
        Assert.Equal(20, sheet.Rows.Single(r => r.StudentId == school.Students[0]).Total);
        Assert.Equal(22, sheet.Rows.Single(r => r.StudentId == school.Students[1]).Total);

        (await SaveAsync(school.SubjectTeacher, school, Row(school.Students[0], null))).EnsureSuccessStatusCode();
        Assert.Null((await GetSheetAsync(school.SubjectTeacher, school)).Rows.Single(r => r.StudentId == school.Students[0]).Total);
    }

    [Fact]
    public async Task Out_of_range_scores_are_rejected_per_cell_and_nothing_is_saved()
    {
        var school = await SetUpAsync();
        var cat = (await GetSheetAsync(school.SubjectTeacher, school)).Components.Single(c => c.Name == "CAT");

        var response = await SaveAsync(school.SubjectTeacher, school,
            new { studentId = school.Students[0], scores = new[] { new { componentId = cat.Id, score = (decimal?)20 } } },
            new { studentId = school.Students[1], scores = new[] { new { componentId = cat.Id, score = (decimal?)31 } } });

        var errors = (await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation")).GetProperty("errors");
        Assert.Equal("CAT must be between 0 and 30.", errors.GetProperty($"entries[1].scores.{cat.Id}")[0].GetString());
        Assert.Null((await GetSheetAsync(school.SubjectTeacher, school)).Rows.Single(r => r.StudentId == school.Students[0]).Total);
    }

    [Fact]
    public async Task Students_outside_the_class_cannot_be_scored_through_it()
    {
        var school = await SetUpAsync();
        var other = await SetUpAsync();
        var cat = (await GetSheetAsync(school.SubjectTeacher, school)).Components.Single(c => c.Name == "CAT");

        var response = await SaveAsync(school.SubjectTeacher, school,
            new { studentId = other.Students[0], scores = new[] { new { componentId = cat.Id, score = (decimal?)10 } } });

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
    }

    // ---------- E5.2 publish lock ----------

    [Fact]
    public async Task Scores_of_a_published_report_are_locked()
    {
        var school = await SetUpAsync();
        var cat = (await GetSheetAsync(school.SubjectTeacher, school)).Components.Single(c => c.Name == "CAT");
        await Api.WithServicesAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            db.TermReports.Add(new TermReport { StudentId = school.Students[0], TermId = school.TermId, ClassRoomId = school.ClassRoomId, Status = ReportStatus.Published, PublishedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        });

        var sheet = await GetSheetAsync(school.SubjectTeacher, school);
        Assert.True(sheet.Rows.Single(r => r.StudentId == school.Students[0]).Locked);
        Assert.False(sheet.Rows.Single(r => r.StudentId == school.Students[1]).Locked);

        var response = await SaveAsync(school.SubjectTeacher, school,
            new { studentId = school.Students[0], scores = new[] { new { componentId = cat.Id, score = (decimal?)10 } } });
        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "assessment.report_published");

        // Admins are bound by the lock too; they unpublish first.
        var admin = await Api.ClientAsync(Roles.Admin);
        await (await SaveAsync(admin, school, new { studentId = school.Students[0], scores = new[] { new { componentId = cat.Id, score = (decimal?)10 } } }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "assessment.report_published");
    }

    // ---------- Row-level access: teacher ↔ class/subject ----------

    [Fact]
    public async Task A_teacher_cannot_grade_a_subject_or_class_they_are_not_assigned()
    {
        var school = await SetUpAsync();
        var (outsider, _) = await TeacherAsync();

        await (await outsider.GetAsync(SheetUrl(school))).AssertProblemAsync(HttpStatusCode.Forbidden, "assessment.not_assigned");
        await (await SaveAsync(outsider, school, new { studentId = school.Students[0], scores = Array.Empty<object>() }))
            .AssertProblemAsync(HttpStatusCode.Forbidden, "assessment.not_assigned");

        // The subject teacher can't grade the class's other subject…
        var otherSubject = school with { SubjectId = school.OtherSubjectId };
        await (await school.SubjectTeacher.GetAsync(SheetUrl(otherSubject))).AssertProblemAsync(HttpStatusCode.Forbidden);
        // …but the form teacher can grade every subject of their own class.
        Assert.Equal(HttpStatusCode.OK, (await school.FormTeacher.GetAsync(SheetUrl(otherSubject))).StatusCode);
    }

    [Theory]
    [InlineData(Roles.Bursar)]
    [InlineData(Roles.Parent)]
    [InlineData(Roles.Student)]
    public async Task Non_teaching_roles_cannot_open_or_save_grade_sheets(string role)
    {
        var school = await SetUpAsync();
        var client = await Api.ClientAsync(role);

        await (await client.GetAsync(SheetUrl(school))).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await SaveAsync(client, school, new { studentId = school.Students[0], scores = Array.Empty<object>() })).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await client.GetAsync("/api/assessment/settings")).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task My_classes_lists_only_what_the_teacher_can_grade()
    {
        var school = await SetUpAsync();

        var subjectTeacher = (await school.SubjectTeacher.GetFromJsonAsync<List<GradableClass>>("/api/assessment/my-classes", Json.Options))!;
        var formTeacher = (await school.FormTeacher.GetFromJsonAsync<List<GradableClass>>("/api/assessment/my-classes", Json.Options))!;

        Assert.Equal(school.SubjectId, Assert.Single(subjectTeacher).SubjectId);
        Assert.False(subjectTeacher[0].IsFormClass);
        Assert.Equal(2, formTeacher.Count);
        Assert.All(formTeacher, c => Assert.True(c.IsFormClass));
    }

    [Fact]
    public async Task A_subject_the_class_does_not_offer_is_404()
    {
        var school = await SetUpAsync();
        var admin = await Api.ClientAsync(Roles.Admin);
        var notOffered = (await admin.GetFromJsonAsync<List<SubjectDto>>("/api/academics/subjects", Json.Options))!
            .First(s => s.Id != school.SubjectId && s.Id != school.OtherSubjectId).Id;

        await (await admin.GetAsync(SheetUrl(school with { SubjectId = notOffered }))).AssertProblemAsync(HttpStatusCode.NotFound, "assessment.subject_not_offered");
    }

    // ---------- E2.4 settings ----------

    [Fact]
    public async Task Settings_rules_and_history_protection()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var original = (await admin.GetFromJsonAsync<AssessmentSettings>("/api/assessment/settings", Json.Options))!;
        try
        {
            object Body(IEnumerable<object> components) => new { components, bands = original.Bands };
            var current = original.Components.Where(c => c.IsActive).ToList();

            // Must total 100.
            var short95 = current.Select(c => (object)new { id = c.Id, name = c.Name, maxScore = c.Name == "Exams" ? 35 : c.MaxScore, isActive = true });
            var bad = await admin.PutAsJsonAsync("/api/assessment/settings", Body(short95));
            Assert.True((await bad.AssertProblemAsync(HttpStatusCode.BadRequest, "validation")).GetProperty("errors").TryGetProperty("components", out _));

            // A component with scores can't have its maximum cut below them.
            var school = await SetUpAsync();
            var cat = current.Single(c => c.Name == "CAT");
            (await SaveAsync(school.SubjectTeacher, school, new { studentId = school.Students[0], scores = new[] { new { componentId = cat.Id, score = (decimal?)28 } } })).EnsureSuccessStatusCode();
            var squeezed = current.Select(c => (object)new
            {
                id = c.Id, name = c.Name, isActive = true,
                maxScore = c.Name == "CAT" ? 20 : c.Name == "Exams" ? 50 : c.MaxScore,
            });
            await (await admin.PutAsJsonAsync("/api/assessment/settings", Body(squeezed))).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "assessment.max_below_scores");

            // Removing a component with scores keeps it, inactive; the rest must still total 100.
            var withoutCat = current.Where(c => c.Name != "CAT").Select(c => (object)new { id = c.Id, name = c.Name, maxScore = c.Name == "Exams" ? 70 : c.MaxScore, isActive = true });
            var saved = await admin.PutAsJsonAsync("/api/assessment/settings", Body(withoutCat));
            saved.EnsureSuccessStatusCode();
            var after = (await saved.Content.ReadFromJsonAsync<AssessmentSettings>(Json.Options))!;
            var keptCat = after.Components.Single(c => c.Id == cat.Id);
            Assert.False(keptCat.IsActive);
            Assert.True(keptCat.HasScores);

            // Teachers read settings but can't change them.
            var teacher = await Api.ClientAsync(Roles.Teacher);
            Assert.Equal(HttpStatusCode.OK, (await teacher.GetAsync("/api/assessment/settings")).StatusCode);
            await (await teacher.PutAsJsonAsync("/api/assessment/settings", Body(withoutCat))).AssertProblemAsync(HttpStatusCode.Forbidden);
        }
        finally
        {
            // Other tests rely on the default components: put them back exactly.
            var restore = new
            {
                components = original.Components.Where(c => c.IsActive).Select(c => new { id = c.Id, name = c.Name, maxScore = c.MaxScore, isActive = true }),
                bands = original.Bands,
            };
            (await admin.PutAsJsonAsync("/api/assessment/settings", restore)).EnsureSuccessStatusCode();
        }
    }

    // ---------- helpers ----------

    private sealed record School(
        Guid ClassRoomId, Guid SubjectId, Guid OtherSubjectId, Guid TermId, IReadOnlyList<Guid> Students,
        HttpClient SubjectTeacher, HttpClient FormTeacher);

    private static string SheetUrl(School s) => $"/api/assessment/gradesheet?classRoomId={s.ClassRoomId}&subjectId={s.SubjectId}&termId={s.TermId}";

    private static async Task<GradeSheet> GetSheetAsync(HttpClient client, School s)
    {
        var response = await client.GetAsync(SheetUrl(s));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GradeSheet>(Json.Options))!;
    }

    private static Task<HttpResponseMessage> SaveAsync(HttpClient client, School s, params object[] entries) =>
        client.PutAsJsonAsync("/api/assessment/gradesheet", new { classRoomId = s.ClassRoomId, subjectId = s.SubjectId, termId = s.TermId, entries });

    /// <summary>A class with 3 students offering two subjects: one taught by a subject teacher, plus a separate form teacher.</summary>
    private async Task<School> SetUpAsync()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var (subjectTeacher, subjectStaff) = await TeacherAsync();
        var (formTeacher, formStaff) = await TeacherAsync();

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

    /// <summary>A teacher with a staff record and a signed-in client.</summary>
    private async Task<(HttpClient Client, StaffDetail Staff)> TeacherAsync()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var email = ApiFactory.UniqueEmail();
        var created = (await (await admin.PostAsJsonAsync("/api/staff", new { fullName = "Teacher " + email[..6], email, roles = new[] { "Teacher" } }))
            .Content.ReadFromJsonAsync<StaffCreated>(Json.Options))!;
        var first = await Api.LoginAsync(email, created.Credentials!.TemporaryPassword);
        var changed = await Api.CreateClient(first.AccessToken).PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = created.Credentials.TemporaryPassword, newPassword = "Brand#NewPass2026" });
        var session = (await changed.Content.ReadFromJsonAsync<AuthResponse>(Json.Options))!;
        return (Api.CreateClient(session.AccessToken), created.Staff);
    }
}
