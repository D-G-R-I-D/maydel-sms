using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Academics;
using Maydel.Application.Assessment;
using Maydel.Application.Reports;
using Maydel.Domain.Academics;
using Maydel.Domain.Assessment;
using Maydel.Domain.Common;
using UglyToad.PdfPig;

namespace Maydel.Api.Tests.Reports;

/// <summary>Term comparison (second term vs first, third vs both, annual average) and PDF report cards (E5.6, E5.6b, E5.8).</summary>
[Collection(nameof(ApiCollection))]
public sealed class ReportPdfAndComparisonTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task Second_term_report_compares_each_subject_and_the_average_with_first_term()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        var (first, second, third) = await TermsAsync(admin);

        // First term, published: subject A 60, subject B 70 → average 65.
        await ScoreAsync(admin, s, first, s.SubjectId, (s.Students[0], 20, 40));
        await ScoreAsync(admin, s, first, s.OtherSubjectId, (s.Students[0], 30, 40));
        await PublishAsync(admin, s, first);

        // Second term draft: subject A 70 (+10), subject B 69.5 (−0.5 → steady) → average 69.75.
        await ScoreAsync(admin, s, second, s.SubjectId, (s.Students[0], 30, 40));
        await ScoreAsync(admin, s, second, s.OtherSubjectId, (s.Students[0], 29.5m, 40));
        var card = await CardAsync(admin, s.Students[0], second);

        var previous = Assert.Single(card.PreviousTerms);
        Assert.Equal((TermNumber.First, 65m), (previous.Number, previous.Average));
        var a = card.Subjects.Single(l => l.SubjectId == s.SubjectId);
        Assert.Equal((10m, Trend.Improved), (a.ChangeFromLastTerm!.Value, a.Trend!.Value));
        Assert.Equal(60m, Assert.Single(a.PreviousTerms).Total);
        var b = card.Subjects.Single(l => l.SubjectId == s.OtherSubjectId);
        Assert.Equal((-0.5m, Trend.Steady), (b.ChangeFromLastTerm!.Value, b.Trend!.Value));
        Assert.Equal((4.75m, Trend.Improved), (card.AverageChange!.Value, card.Trend!.Value));
        Assert.Equal(67.38m, card.CumulativeAverage); // (65 + 69.75) / 2, rounded half up
        Assert.Equal(139.5m, card.TotalScore);

        // Third term compares with both earlier published terms.
        await AttendanceAsync(admin, s, second);
        (await admin.PostAsJsonAsync("/api/reports/publish", new { classRoomId = s.ClassRoomId, termId = second })).EnsureSuccessStatusCode();
        await ScoreAsync(admin, s, third, s.SubjectId, (s.Students[0], 10, 40)); // 50: down 20 from 70
        var thirdCard = await CardAsync(admin, s.Students[0], third);
        Assert.Equal([TermNumber.First, TermNumber.Second], thirdCard.PreviousTerms.Select(p => p.Number));
        var thirdLine = thirdCard.Subjects.Single();
        Assert.Equal((-20m, Trend.Declined), (thirdLine.ChangeFromLastTerm!.Value, thirdLine.Trend!.Value));
        Assert.Equal([60m, 70m], thirdLine.PreviousTerms.Select(p => p.Total));
    }

    [Fact]
    public async Task Unpublished_earlier_terms_are_never_used_for_comparison()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        var (first, second, _) = await TermsAsync(admin);
        await ScoreAsync(admin, s, first, s.SubjectId, (s.Students[0], 20, 40)); // draft only
        await ScoreAsync(admin, s, second, s.SubjectId, (s.Students[0], 30, 40));

        var card = await CardAsync(admin, s.Students[0], second);

        Assert.Empty(card.PreviousTerms);
        Assert.Null(card.Trend);
        Assert.Null(card.CumulativeAverage);
        Assert.Empty(card.Subjects.Single().PreviousTerms);
    }

    [Fact]
    public async Task Report_card_pdf_is_one_a4_page_with_the_students_results_and_a_draft_watermark()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        var (first, second, _) = await TermsAsync(admin);
        await ScoreAsync(admin, s, first, s.SubjectId, (s.Students[0], 20, 40));
        await PublishAsync(admin, s, first);
        await ScoreAsync(admin, s, second, s.SubjectId, (s.Students[0], 25, 40));

        var response = await admin.GetAsync($"/api/reports/{s.Students[0]}/{second}/pdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.EndsWith(".pdf", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        using var pdf = PdfDocument.Open(await response.Content.ReadAsByteArrayAsync());
        Assert.Equal(1, pdf.NumberOfPages);
        var page = pdf.GetPage(1);
        Assert.InRange(page.Width, 594, 597);   // A4 portrait, points
        var text = page.Text;
        Assert.Contains("Adaeze", text);
        Assert.Contains("DRAFT", text);
        Assert.Contains("TERM COMPARISON", text);
        Assert.Contains("MAYDEL SPRING SCHOOLS", text);

        // Published first term: no watermark.
        var published = await admin.GetAsync($"/api/reports/{s.Students[0]}/{first}/pdf");
        using var publishedPdf = PdfDocument.Open(await published.Content.ReadAsByteArrayAsync());
        Assert.DoesNotContain("DRAFT", publishedPdf.GetPage(1).Text);
    }

    [Fact]
    public async Task Class_pdf_has_a_page_per_student_with_results()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        var (first, _, _) = await TermsAsync(admin);
        await ScoreAsync(admin, s, first, s.SubjectId, (s.Students[0], 20, 40), (s.Students[1], 25, 30)); // 3rd student: no scores

        var response = await s.FormTeacher.GetAsync($"/api/reports/class/pdf?classRoomId={s.ClassRoomId}&termId={first}");

        response.EnsureSuccessStatusCode();
        using var pdf = PdfDocument.Open(await response.Content.ReadAsByteArrayAsync());
        Assert.Equal(2, pdf.NumberOfPages);
    }

    [Fact]
    public async Task Pdf_downloads_follow_the_report_access_rules()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var (outsider, _) = await SchoolBuilder.TeacherAsync(Api);
        var parent = await Api.ClientAsync(Roles.Parent);
        var term = (await (await Api.ClientAsync(Roles.Admin)).GetFromJsonAsync<TermDto>("/api/academics/terms/current", Json.Options))!.Id;

        await (await outsider.GetAsync($"/api/reports/{s.Students[0]}/{term}/pdf")).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await outsider.GetAsync($"/api/reports/class/pdf?classRoomId={s.ClassRoomId}&termId={term}")).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await parent.GetAsync($"/api/reports/{s.Students[0]}/{term}/pdf")).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    // ---------- helpers ----------

    private static async Task<(Guid First, Guid Second, Guid Third)> TermsAsync(HttpClient admin)
    {
        var session = (await admin.GetFromJsonAsync<List<SessionDto>>("/api/academics/sessions", Json.Options))!.Single(x => x.Name == "2026/2027");
        Guid Id(TermNumber n) => session.Terms.Single(t => t.Number == n).Id;
        return (Id(TermNumber.First), Id(TermNumber.Second), Id(TermNumber.Third));
    }

    private static async Task ScoreAsync(HttpClient client, School s, Guid termId, Guid subjectId, params (Guid Student, decimal Cat, decimal Exams)[] rows)
    {
        var settings = (await client.GetFromJsonAsync<AssessmentSettings>("/api/assessment/settings", Json.Options))!;
        var cat = settings.Components.Single(c => c.Name == "CAT" && c.IsActive).Id;
        var exams = settings.Components.Single(c => c.Name == "Exams" && c.IsActive).Id;
        (await client.PutAsJsonAsync("/api/assessment/gradesheet", new
        {
            classRoomId = s.ClassRoomId, subjectId, termId,
            entries = rows.Select(r => new { studentId = r.Student, scores = new[] { new { componentId = cat, score = (decimal?)r.Cat }, new { componentId = exams, score = (decimal?)r.Exams } } }),
        })).EnsureSuccessStatusCode();
    }

    private static async Task AttendanceAsync(HttpClient admin, School s, Guid termId) =>
        (await admin.PutAsJsonAsync("/api/reports/attendance", new
        {
            classRoomId = s.ClassRoomId, termId, daysOpened = 60,
            entries = s.Students.Select(id => new { studentId = id, daysPresent = 58 }),
        })).EnsureSuccessStatusCode();

    private static async Task PublishAsync(HttpClient admin, School s, Guid termId)
    {
        await AttendanceAsync(admin, s, termId);
        (await admin.PostAsJsonAsync("/api/reports/publish", new { classRoomId = s.ClassRoomId, termId })).EnsureSuccessStatusCode();
    }

    private static async Task<ReportCard> CardAsync(HttpClient client, Guid studentId, Guid termId)
    {
        var response = await client.GetAsync($"/api/reports/{studentId}/{termId}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReportCard>(Json.Options))!;
    }
}
