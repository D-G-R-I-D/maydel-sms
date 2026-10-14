using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Assessment;
using Maydel.Application.Reports;
using Maydel.Domain.Assessment;
using Maydel.Domain.Common;

namespace Maydel.Api.Tests.Reports;

/// <summary>E5.3 comments, E5.4 report preview, E5.5 publish/unpublish — with who may do what.</summary>
[Collection(nameof(ApiCollection))]
public sealed class ReportTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task Preview_shows_live_figures_then_publishing_freezes_them_and_locks_everything()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        // CAT /30 + Exams /40.  Adaeze: 70 & 50 → 60.  Bola: 50 & 40 → 45.  Chidi: no scores.
        await ScoreAsync(admin, s, s.SubjectId, (s.Students[0], 30, 40), (s.Students[1], 20, 30));
        await ScoreAsync(admin, s, s.OtherSubjectId, (s.Students[0], 20, 30), (s.Students[1], 10, 30));

        var draft = await CardAsync(admin, s.Students[0], s.TermId);
        Assert.Equal(ReportStatus.Draft, draft.Status);
        Assert.Equal(60m, draft.Average);
        Assert.Equal(1, draft.Position);
        Assert.Equal(52.5m, draft.ClassAverage);
        Assert.Equal(2, draft.ClassSize);
        Assert.Null(draft.DaysOpened); // attendance not entered yet
        var line = draft.Subjects.Single(l => l.SubjectId == s.SubjectId);
        Assert.Equal((70m, 50m, 60m), (line.ClassHighest, line.ClassLowest, line.ClassAverage));
        Assert.Equal("B", line.Grade); // 70 on the school standard scale
        Assert.NotEmpty(draft.GradingKey);
        Assert.NotNull(draft.NextTermBegins);

        // Can't publish without attendance for everyone being published.
        await (await admin.PostAsJsonAsync("/api/reports/publish", new { classRoomId = s.ClassRoomId, termId = s.TermId }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "report.attendance_missing");

        await AttendanceAsync(s.FormTeacher, s, 60, (s.Students[0], 58), (s.Students[1], 60), (s.Students[2], 55));
        (await s.FormTeacher.PutAsJsonAsync($"/api/reports/{s.Students[0]}/{s.TermId}/form-teacher-comment", new { comment = "A diligent pupil." })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/reports/{s.Students[0]}/{s.TermId}/principal-comment", new { comment = "Keep it up." })).EnsureSuccessStatusCode();

        var publish = await admin.PostAsJsonAsync("/api/reports/publish", new { classRoomId = s.ClassRoomId, termId = s.TermId });
        publish.EnsureSuccessStatusCode();
        var result = (await publish.Content.ReadFromJsonAsync<PublishResult>(Json.Options))!;
        Assert.Equal(2, result.Published);
        Assert.Equal(s.Students[2], Assert.Single(result.Skipped).StudentId); // no scores

        var published = await CardAsync(admin, s.Students[0], s.TermId);
        Assert.Equal(ReportStatus.Published, published.Status);
        Assert.Equal((60m, 1, 52.5m, 2), (published.Average!.Value, published.Position!.Value, published.ClassAverage, published.ClassSize));
        Assert.Equal((60, 58, 2), (published.DaysOpened!.Value, published.DaysPresent!.Value, published.DaysAbsent!.Value));
        Assert.Equal(("A diligent pupil.", "Keep it up."), (published.FormTeacherComment, published.PrincipalComment));

        // Everything that feeds the report is locked now.
        var cat = await ComponentAsync(admin, "CAT");
        await (await SaveScoresAsync(admin, s, s.SubjectId, (s.Students[0], cat, 1))).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "assessment.report_published");
        await (await s.FormTeacher.PutAsJsonAsync($"/api/reports/{s.Students[0]}/{s.TermId}/form-teacher-comment", new { comment = "Changed" }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "report.published");
        await (await AttendanceRequestAsync(s.FormTeacher, s, 61, (s.Students[0], 61))).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "report.published");

        var list = (await admin.GetFromJsonAsync<ClassReportSummary>($"/api/reports/class?classRoomId={s.ClassRoomId}&termId={s.TermId}", Json.Options))!;
        Assert.Equal((2, 1, 60), (list.PublishedCount, list.DraftCount, list.DaysOpened!.Value));
    }

    [Fact]
    public async Task Unpublish_reopens_and_republishing_recalculates()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        await ScoreAsync(admin, s, s.SubjectId, (s.Students[0], 25, 40), (s.Students[1], 20, 40));
        await AttendanceAsync(admin, s, 50, (s.Students[0], 50), (s.Students[1], 49));
        (await admin.PostAsJsonAsync("/api/reports/publish", new { classRoomId = s.ClassRoomId, termId = s.TermId })).EnsureSuccessStatusCode();
        Assert.Equal(2, (await CardAsync(admin, s.Students[1], s.TermId)).Position);

        var unpublish = await admin.PostAsJsonAsync("/api/reports/unpublish", new { classRoomId = s.ClassRoomId, termId = s.TermId, studentIds = (Guid[]?)null });
        Assert.Equal(2, (await unpublish.Content.ReadFromJsonAsync<UnpublishResult>(Json.Options))!.Unpublished);

        // Bola now beats Adaeze; publishing again picks it up.
        await ScoreAsync(admin, s, s.SubjectId, (s.Students[1], 30, 40));
        (await admin.PostAsJsonAsync("/api/reports/publish", new { classRoomId = s.ClassRoomId, termId = s.TermId })).EnsureSuccessStatusCode();
        var bola = await CardAsync(admin, s.Students[1], s.TermId);
        Assert.Equal((ReportStatus.Published, 1, 70m), (bola.Status, bola.Position!.Value, bola.Average!.Value));
    }

    [Fact]
    public async Task Tied_averages_share_a_position()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        await ScoreAsync(admin, s, s.SubjectId, (s.Students[0], 30, 40), (s.Students[1], 30, 40), (s.Students[2], 10, 10));

        var list = (await admin.GetFromJsonAsync<ClassReportSummary>($"/api/reports/class?classRoomId={s.ClassRoomId}&termId={s.TermId}", Json.Options))!;

        Assert.Equal([1, 1, 3], list.Rows.OrderBy(r => r.Position).Select(r => r.Position!.Value));
    }

    // ---------- who may do what ----------

    [Fact]
    public async Task Only_the_form_teacher_or_an_admin_writes_the_form_comment_and_attendance()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);

        await (await s.SubjectTeacher.PutAsJsonAsync($"/api/reports/{s.Students[0]}/{s.TermId}/form-teacher-comment", new { comment = "Hi" }))
            .AssertProblemAsync(HttpStatusCode.Forbidden, "report.not_form_teacher");
        await (await AttendanceRequestAsync(s.SubjectTeacher, s, 60, (s.Students[0], 60))).AssertProblemAsync(HttpStatusCode.Forbidden, "report.not_form_teacher");
        // The principal's comment is admin-only, even for the form teacher.
        await (await s.FormTeacher.PutAsJsonAsync($"/api/reports/{s.Students[0]}/{s.TermId}/principal-comment", new { comment = "Hi" }))
            .AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Teachers_of_the_class_can_view_reports_others_cannot()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var (outsider, _) = await SchoolBuilder.TeacherAsync(Api);
        var card = $"/api/reports/{s.Students[0]}/{s.TermId}";
        var list = $"/api/reports/class?classRoomId={s.ClassRoomId}&termId={s.TermId}";

        Assert.Equal(HttpStatusCode.OK, (await s.SubjectTeacher.GetAsync(card)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.FormTeacher.GetAsync(list)).StatusCode);
        await (await outsider.GetAsync(card)).AssertProblemAsync(HttpStatusCode.Forbidden, "report.not_allowed");
        await (await outsider.GetAsync(list)).AssertProblemAsync(HttpStatusCode.Forbidden, "report.not_allowed");
    }

    [Theory]
    [InlineData(Roles.Bursar)]
    [InlineData(Roles.Parent)]
    [InlineData(Roles.Student)]
    public async Task Other_roles_cannot_see_or_publish_reports_through_the_staff_api(string role)
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var client = await Api.ClientAsync(role);

        await (await client.GetAsync($"/api/reports/{s.Students[0]}/{s.TermId}")).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await client.PostAsJsonAsync("/api/reports/publish", new { classRoomId = s.ClassRoomId, termId = s.TermId })).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Teachers_cannot_publish_or_unpublish()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        await (await s.FormTeacher.PostAsJsonAsync("/api/reports/publish", new { classRoomId = s.ClassRoomId, termId = s.TermId })).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await s.FormTeacher.PostAsJsonAsync("/api/reports/unpublish", new { classRoomId = s.ClassRoomId, termId = s.TermId })).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Publishing_a_class_with_no_scores_is_refused()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        await (await admin.PostAsJsonAsync("/api/reports/publish", new { classRoomId = s.ClassRoomId, termId = s.TermId }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "report.nothing_to_publish");
    }

    [Fact]
    public async Task Attendance_cannot_exceed_days_opened()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var response = await AttendanceRequestAsync(s.FormTeacher, s, 60, (s.Students[0], 61));
        var errors = (await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation")).GetProperty("errors");
        Assert.True(errors.TryGetProperty("entries[0].daysPresent", out _));
    }

    // ---------- helpers ----------

    private static async Task<Guid> ComponentAsync(HttpClient client, string name) =>
        (await client.GetFromJsonAsync<AssessmentSettings>("/api/assessment/settings", Json.Options))!.Components.Single(c => c.Name == name && c.IsActive).Id;

    /// <summary>Sets CAT and Exams for each (student, cat, exams).</summary>
    private static async Task ScoreAsync(HttpClient client, School s, Guid subjectId, params (Guid Student, decimal Cat, decimal Exams)[] rows)
    {
        var cat = await ComponentAsync(client, "CAT");
        var exams = await ComponentAsync(client, "Exams");
        var response = await client.PutAsJsonAsync("/api/assessment/gradesheet", new
        {
            classRoomId = s.ClassRoomId, subjectId, termId = s.TermId,
            entries = rows.Select(r => new { studentId = r.Student, scores = new[] { new { componentId = cat, score = (decimal?)r.Cat }, new { componentId = exams, score = (decimal?)r.Exams } } }),
        });
        response.EnsureSuccessStatusCode();
    }

    private static Task<HttpResponseMessage> SaveScoresAsync(HttpClient client, School s, Guid subjectId, (Guid Student, Guid Component, decimal Score) row) =>
        client.PutAsJsonAsync("/api/assessment/gradesheet", new
        {
            classRoomId = s.ClassRoomId, subjectId, termId = s.TermId,
            entries = new[] { new { studentId = row.Student, scores = new[] { new { componentId = row.Component, score = (decimal?)row.Score } } } },
        });

    private static Task<HttpResponseMessage> AttendanceRequestAsync(HttpClient client, School s, int daysOpened, params (Guid Student, int Present)[] rows) =>
        client.PutAsJsonAsync("/api/reports/attendance", new
        {
            classRoomId = s.ClassRoomId, termId = s.TermId, daysOpened,
            entries = rows.Select(r => new { studentId = r.Student, daysPresent = r.Present }),
        });

    private static async Task AttendanceAsync(HttpClient client, School s, int daysOpened, params (Guid Student, int Present)[] rows) =>
        (await AttendanceRequestAsync(client, s, daysOpened, rows)).EnsureSuccessStatusCode();

    private static async Task<ReportCard> CardAsync(HttpClient client, Guid studentId, Guid termId)
    {
        var response = await client.GetAsync($"/api/reports/{studentId}/{termId}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReportCard>(Json.Options))!;
    }
}
