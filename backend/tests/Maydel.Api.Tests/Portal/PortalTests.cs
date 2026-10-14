using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Academics;
using Maydel.Application.Assessment;
using Maydel.Application.Identity;
using Maydel.Application.Portal;
using Maydel.Application.Reports;
using Maydel.Application.Students;
using Maydel.Domain.Academics;
using Maydel.Domain.Assessment;
using Maydel.Domain.Common;
using UglyToad.PdfPig;

namespace Maydel.Api.Tests.Portal;

/// <summary>M1.3 portal: parents see only their own active children, students only themselves, and only published results.</summary>
[Collection(nameof(ApiCollection))]
public sealed class PortalTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task Parents_see_their_children_and_only_published_results()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        var parent = await ParentOfAsync(admin, s.Students[0], s.Students[1]);
        var (first, second) = await TermsAsync(admin);
        await ScoreAsync(admin, s, first, (s.Students[0], 20, 40), (s.Students[1], 25, 35), (s.Students[2], 30, 30));
        await PublishAsync(admin, s, first);
        await ScoreAsync(admin, s, second, (s.Students[0], 28, 40)); // draft

        var children = (await parent.GetFromJsonAsync<List<PortalChild>>("/api/portal/children", Json.Options))!;

        Assert.Equal(["Adaeze Test", "Bola Test"], children.Select(c => c.Name));
        var latest = children[0].LatestResult!;
        Assert.Equal((first, TermNumber.First, 60m), (latest.TermId, latest.Number, latest.Average));
        Assert.NotNull(latest.Position);

        var results = (await parent.GetFromJsonAsync<List<PortalResultSummary>>($"/api/portal/children/{s.Students[0]}/results", Json.Options))!;
        Assert.Equal([first], results.Select(r => r.TermId)); // the draft second term isn't listed

        var card = (await parent.GetFromJsonAsync<ReportCard>($"/api/portal/children/{s.Students[0]}/results/{first}", Json.Options))!;
        Assert.Equal((ReportStatus.Published, (decimal?)60m), (card.Status, card.Average));
        await (await parent.GetAsync($"/api/portal/children/{s.Students[0]}/results/{second}")).AssertProblemAsync(HttpStatusCode.NotFound, "report.not_published");

        var pdf = await parent.GetAsync($"/api/portal/children/{s.Students[0]}/results/{first}/pdf");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        using var document = PdfDocument.Open(await pdf.Content.ReadAsByteArrayAsync());
        Assert.DoesNotContain("DRAFT", document.GetPage(1).Text);
        await (await parent.GetAsync($"/api/portal/children/{s.Students[0]}/results/{second}/pdf")).AssertProblemAsync(HttpStatusCode.NotFound, "report.not_published");
    }

    [Fact]
    public async Task Another_familys_child_is_not_found_everywhere()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        var parent = await ParentOfAsync(admin, s.Students[0]);
        var (first, _) = await TermsAsync(admin);
        await ScoreAsync(admin, s, first, (s.Students[0], 20, 40), (s.Students[2], 30, 30));
        await PublishAsync(admin, s, first);
        var other = s.Students[2];

        Assert.DoesNotContain((await parent.GetFromJsonAsync<List<PortalChild>>("/api/portal/children", Json.Options))!, c => c.StudentId == other);
        foreach (var url in new[]
        {
            $"/api/portal/children/{other}/results", $"/api/portal/children/{other}/results/{first}",
            $"/api/portal/children/{other}/results/{first}/pdf", $"/api/portal/children/{other}/photo",
        })
            await (await parent.GetAsync(url)).AssertProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Students_see_only_themselves()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        var student = await StudentClientAsync(admin, s.Students[1]);

        var children = (await student.GetFromJsonAsync<List<PortalChild>>("/api/portal/children", Json.Options))!;

        Assert.Equal(s.Students[1], Assert.Single(children).StudentId);
        Assert.Null(children[0].LatestResult);
        await (await student.GetAsync($"/api/portal/children/{s.Students[0]}/results")).AssertProblemAsync(HttpStatusCode.NotFound, "portal.student_not_found");
    }

    [Fact]
    public async Task Suspended_children_drop_out_of_the_portal()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        var parent = await ParentOfAsync(admin, s.Students[0], s.Students[1]);

        (await admin.PostAsJsonAsync($"/api/students/{s.Students[1]}/status", new { status = "Suspended" })).EnsureSuccessStatusCode();

        Assert.Equal([s.Students[0]], (await parent.GetFromJsonAsync<List<PortalChild>>("/api/portal/children", Json.Options))!.Select(c => c.StudentId));
        await (await parent.GetAsync($"/api/portal/children/{s.Students[1]}/results")).AssertProblemAsync(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Teacher)]
    [InlineData(Roles.Bursar)]
    public async Task Staff_use_the_staff_api_not_the_portal(string role) =>
        await (await (await Api.ClientAsync(role)).GetAsync("/api/portal/children")).AssertProblemAsync(HttpStatusCode.Forbidden);

    // ---------- helpers ----------

    private static int _phone = Random.Shared.Next(1_000_000, 8_000_000);

    /// <summary>A guardian linked to these students, signed in with a changed password.</summary>
    private async Task<HttpClient> ParentOfAsync(HttpClient admin, params Guid[] students)
    {
        var phone = $"0807{Interlocked.Increment(ref _phone):0000000}";
        Guid guardianId = default;
        foreach (var id in students)
        {
            var added = await admin.PostAsJsonAsync($"/api/students/{id}/guardians",
                new { fullName = "Mrs. Portal Parent", phone, relationship = "Mother", isPrimaryContact = true, canPickUp = true });
            added.EnsureSuccessStatusCode();
            guardianId = (await added.Content.ReadFromJsonAsync<GuardianAdded>(Json.Options))!.GuardianId;
        }
        var issued = (await (await admin.PostAsync($"/api/guardians/{guardianId}/login", null)).Content.ReadFromJsonAsync<GuardianLoginIssued>(Json.Options))!;
        return await ChangedPasswordClientAsync(issued.Credentials!);
    }

    private async Task<HttpClient> StudentClientAsync(HttpClient admin, Guid studentId)
    {
        var issued = (await (await admin.PostAsync($"/api/students/{studentId}/login", null)).Content.ReadFromJsonAsync<IssuedCredentials>(Json.Options))!;
        return await ChangedPasswordClientAsync(issued);
    }

    private async Task<HttpClient> ChangedPasswordClientAsync(IssuedCredentials credentials)
    {
        var first = await Api.LoginAsync(credentials.UserName, credentials.TemporaryPassword);
        var change = await Api.CreateClient(first.AccessToken).PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = credentials.TemporaryPassword, newPassword = "Family#NewPass2026" });
        change.EnsureSuccessStatusCode();
        return Api.CreateClient((await change.Content.ReadFromJsonAsync<AuthResponse>(Json.Options))!.AccessToken);
    }

    private static async Task<(Guid First, Guid Second)> TermsAsync(HttpClient admin)
    {
        var session = (await admin.GetFromJsonAsync<List<SessionDto>>("/api/academics/sessions", Json.Options))!.Single(x => x.Name == "2026/2027");
        return (session.Terms.Single(t => t.Number == TermNumber.First).Id, session.Terms.Single(t => t.Number == TermNumber.Second).Id);
    }

    private static async Task ScoreAsync(HttpClient admin, School s, Guid termId, params (Guid Student, decimal Cat, decimal Exams)[] rows)
    {
        var settings = (await admin.GetFromJsonAsync<AssessmentSettings>("/api/assessment/settings", Json.Options))!;
        var cat = settings.Components.Single(c => c.Name == "CAT" && c.IsActive).Id;
        var exams = settings.Components.Single(c => c.Name == "Exams" && c.IsActive).Id;
        (await admin.PutAsJsonAsync("/api/assessment/gradesheet", new
        {
            classRoomId = s.ClassRoomId, subjectId = s.SubjectId, termId,
            entries = rows.Select(r => new { studentId = r.Student, scores = new[] { new { componentId = cat, score = (decimal?)r.Cat }, new { componentId = exams, score = (decimal?)r.Exams } } }),
        })).EnsureSuccessStatusCode();
    }

    private static async Task PublishAsync(HttpClient admin, School s, Guid termId)
    {
        (await admin.PutAsJsonAsync("/api/reports/attendance", new
        {
            classRoomId = s.ClassRoomId, termId, daysOpened = 60, entries = s.Students.Select(id => new { studentId = id, daysPresent = 58 }),
        })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync("/api/reports/publish", new { classRoomId = s.ClassRoomId, termId })).EnsureSuccessStatusCode();
    }
}
