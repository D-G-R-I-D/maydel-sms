using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Common;
using Maydel.Application.Homework;
using Maydel.Application.Identity;
using Maydel.Application.Notifications;
using Maydel.Domain.Common;
using Maydel.Domain.Communication;
using Maydel.Domain.Homework;
using SkiaSharp;

namespace Maydel.Api.Tests.Homework;

/// <summary>E17.1: teachers set homework for what they teach; students hand in; teachers review; families are notified.</summary>
[Collection(nameof(ApiCollection))]
public sealed class HomeworkTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task A_teacher_sets_homework_a_student_hands_it_in_and_gets_feedback()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var student = await StudentClientAsync(s.Students[0]); // has a login before the homework is set, so it's notified
        var hw = await HomeworkAsync(await s.SubjectTeacher.PostAsJsonAsync("/api/homework", new
        {
            classRoomId = s.ClassRoomId, subjectId = s.SubjectId, title = "Fractions worksheet",
            instructionsHtml = "<p>Do questions <strong>1–10</strong>.</p><script>x()</script>", dueOn = "2026-10-20",
        }));
        Assert.Equal("Do questions 1–10.", hw.Instructions);
        Assert.DoesNotContain("<script", hw.InstructionsHtml);
        Assert.Equal((0, 3), (hw.Submitted, hw.ClassSize));
        hw = await HomeworkAsync(await s.SubjectTeacher.PostAsync($"/api/homework/{hw.Id}/files", Form(("file", "%PDF-1.4\n% worksheet\n"u8.ToArray(), "worksheet.pdf"))));
        var sheet = Assert.Single(hw.Files);
        Assert.Equal(("worksheet.pdf", "application/pdf"), (sheet.FileName, sheet.ContentType));

        var mine = Assert.Single((await student.GetFromJsonAsync<List<MyHomework>>($"/api/portal/children/{s.Students[0]}/homework", Json.Options))!, m => m.Homework.Id == hw.Id);
        Assert.Equal((HandInState.Missing, false), (mine.State, mine.IsOverdue));
        Assert.Null(mine.Homework.Submitted); // class counts are for staff
        var download = await student.GetAsync($"/api/homework/{hw.Id}/files/{sheet.Id}");
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);

        var handedIn = await (await student.PostAsync($"/api/portal/children/{s.Students[0]}/homework/{hw.Id}/hand-in",
            Form(("files", Png(), "answers.png"), ("text", "My answers are attached.")))).Content.ReadFromJsonAsync<MyHomework>(Json.Options);
        Assert.Equal((HandInState.Submitted, false), (handedIn!.State, handedIn.Submission!.IsLate));
        var answer = Assert.Single(handedIn.Submission.Files);

        // The teacher sees the whole class, reviews, and the family hears about it.
        var roster = (await s.SubjectTeacher.GetFromJsonAsync<List<RosterRow>>($"/api/homework/{hw.Id}/hand-ins", Json.Options))!;
        Assert.Equal(3, roster.Count);
        var row = Assert.Single(roster, r => r.State == HandInState.Submitted);
        Assert.Equal(HttpStatusCode.OK, (await s.SubjectTeacher.GetAsync($"/api/homework/{hw.Id}/hand-ins/{row.Submission!.Id}/files/{answer.Id}")).StatusCode);
        (await s.SubjectTeacher.PutAsJsonAsync($"/api/homework/{hw.Id}/hand-ins/{row.Submission.Id}", new { status = "Returned", feedback = "Check question 4." })).EnsureSuccessStatusCode();
        var returned = (await student.GetFromJsonAsync<MyHomework>($"/api/portal/children/{s.Students[0]}/homework/{hw.Id}", Json.Options))!;
        Assert.Equal((HandInState.Returned, "Check question 4."), (returned.State, returned.Submission!.Feedback));
        var kinds = (await student.GetFromJsonAsync<NotificationInbox>("/api/notifications", Json.Options))!.Items.Select(n => n.Kind).ToList();
        Assert.Contains(NotificationKind.HomeworkSet, kinds);
        Assert.Contains(NotificationKind.HomeworkReviewed, kinds);

        await (await s.SubjectTeacher.DeleteAsync($"/api/homework/{hw.Id}")).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "homework.has_submissions");
    }

    [Fact]
    public async Task Only_the_class_s_teachers_and_families_can_reach_its_homework()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var other = await SchoolBuilder.SetUpAsync(Api);
        var body = new { classRoomId = s.ClassRoomId, subjectId = s.OtherSubjectId, title = "Reading", dueOn = "2026-10-20" };

        // The subject teacher doesn't teach the other subject; the form teacher may set anything for the class.
        await (await s.SubjectTeacher.PostAsJsonAsync("/api/homework", body)).AssertProblemAsync(HttpStatusCode.Forbidden, "homework.forbidden");
        var hw = await HomeworkAsync(await s.FormTeacher.PostAsJsonAsync("/api/homework", body));
        await (await other.SubjectTeacher.GetAsync($"/api/homework/{hw.Id}")).AssertProblemAsync(HttpStatusCode.NotFound);
        Assert.DoesNotContain((await other.SubjectTeacher.GetFromJsonAsync<PagedResult<HomeworkDto>>("/api/homework?pageSize=200", Json.Options))!.Items, h => h.Id == hw.Id);
        await (await (await Api.ClientAsync(Roles.Bursar)).GetAsync("/api/homework")).AssertProblemAsync(HttpStatusCode.Forbidden);

        var outsider = await StudentClientAsync(other.Students[0]);
        await (await outsider.GetAsync($"/api/portal/children/{other.Students[0]}/homework/{hw.Id}")).AssertProblemAsync(HttpStatusCode.NotFound);
        await (await outsider.GetAsync($"/api/portal/children/{s.Students[0]}/homework")).AssertProblemAsync(HttpStatusCode.NotFound);
        await (await s.FormTeacher.PostAsJsonAsync("/api/homework", body with { title = "Late", dueOn = "2026-10-01" })).AssertProblemAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Hand_ins_are_checked_and_late_ones_are_marked()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var today = Api.Clock.Today;
        var hw = await HomeworkAsync(await s.SubjectTeacher.PostAsJsonAsync("/api/homework", new
        {
            classRoomId = s.ClassRoomId, subjectId = s.SubjectId, title = "Spelling", dueOn = today.ToString("yyyy-MM-dd"),
        }));
        var paper = await HomeworkAsync(await s.SubjectTeacher.PostAsJsonAsync("/api/homework", new
        {
            classRoomId = s.ClassRoomId, subjectId = s.SubjectId, title = "Draw a map", dueOn = today.ToString("yyyy-MM-dd"), acceptsSubmissions = false,
        }));
        var student = await StudentClientAsync(s.Students[1]);
        var url = $"/api/portal/children/{s.Students[1]}/homework/{hw.Id}/hand-in";

        await (await student.PostAsync(url, new MultipartFormDataContent { { new StringContent(" "), "text" } })).AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        await (await student.PostAsync(url, Form(("files", "<html><script>x</script></html>"u8.ToArray(), "notes.html")))).AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        await (await student.PostAsync($"/api/portal/children/{s.Students[1]}/homework/{paper.Id}/hand-in", Form(("text", "done")))).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "homework.no_submissions");

        var original = Api.Clock.Today;
        try
        {
            Api.Clock.Today = today.AddDays(1);
            var late = await (await student.PostAsync(url, Form(("text", "Sorry it's late")))).Content.ReadFromJsonAsync<MyHomework>(Json.Options);
            Assert.True(late!.Submission!.IsLate);
        }
        finally
        {
            Api.Clock.Today = original;
        }
    }

    // ---------- helpers ----------

    private static async Task<HomeworkDto> HomeworkAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode) Assert.Fail($"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<HomeworkDto>(Json.Options))!;
    }

    private static MultipartFormDataContent Form(params (string Name, byte[] Bytes, string FileName)[] files) => Form(files, []);

    private static MultipartFormDataContent Form((string Name, byte[] Bytes, string FileName) file, params (string Name, string Value)[] fields) => Form([file], fields);

    private static MultipartFormDataContent Form(params (string Name, string Value)[] fields) => Form([], fields);

    private static MultipartFormDataContent Form((string Name, byte[] Bytes, string FileName)[] files, (string Name, string Value)[] fields)
    {
        var form = new MultipartFormDataContent();
        foreach (var (name, value) in fields) form.Add(new StringContent(value), name);
        foreach (var (name, bytes, fileName) in files)
        {
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(content, name, fileName);
        }
        return form;
    }

    private static byte[] Png()
    {
        using var surface = SKSurface.Create(new SKImageInfo(300, 200));
        surface.Canvas.Clear(SKColors.Bisque);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
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
