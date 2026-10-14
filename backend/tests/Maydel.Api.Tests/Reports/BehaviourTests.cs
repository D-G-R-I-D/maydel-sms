using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Academics;
using Maydel.Application.Assessment;
using Maydel.Application.Reports;
using Maydel.Domain.Common;
using UglyToad.PdfPig;

namespace Maydel.Api.Tests.Reports;

/// <summary>Behaviour ratings and clubs on the report (E2.5): who may set them, the lock, settings, and the PDF.</summary>
[Collection(nameof(ApiCollection))]
public sealed class BehaviourTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task Form_teacher_rates_traits_and_clubs_without_changing_any_figures()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        await ScoreAsync(admin, s, s.SubjectId, (s.Students[0], 20, 40), (s.Students[1], 25, 35));
        var before = await CardAsync(admin, s.Students[0], s.TermId);
        var traits = (await TraitsAsync(s.FormTeacher)).Where(t => t.IsActive).ToList();

        var response = await s.FormTeacher.PutAsJsonAsync($"/api/reports/{s.Students[0]}/{s.TermId}/behaviour", new
        {
            ratings = new[] { new { traitId = traits[0].Id, rating = "a" }, new { traitId = traits[1].Id, rating = "B" }, new { traitId = traits[2].Id, rating = "" } },
            clubs = new[] { new { name = "Coding", rating = (string?)"Excellent" }, new { name = "Ballet", rating = (string?)null } },
        });

        response.EnsureSuccessStatusCode();
        var card = (await response.Content.ReadFromJsonAsync<ReportCard>(Json.Options))!;
        Assert.Equal(traits.Count, card.Behaviour.Count);
        Assert.Equal(["A", "B", null], card.Behaviour.Take(3).Select(b => b.Rating));
        Assert.Equal([new ClubLine("Coding", "Excellent"), new ClubLine("Ballet", null)], card.Clubs);
        Assert.Equal(["A", "B", "C", "D", "E"], card.BehaviourKey.Select(k => k.Rating));
        Assert.Equal((before.Average, before.Position, before.ClassAverage, before.TotalScore), (card.Average, card.Position, card.ClassAverage, card.TotalScore));

        var list = (await admin.GetFromJsonAsync<ClassReportSummary>($"/api/reports/class?classRoomId={s.ClassRoomId}&termId={s.TermId}", Json.Options))!;
        Assert.Equal(2, list.Rows.Single(r => r.StudentId == s.Students[0]).TraitsRated);
    }

    [Fact]
    public async Task New_schools_get_the_traits_from_the_excel_sheet()
    {
        var settings = (await (await Api.ClientAsync(Roles.Teacher)).GetFromJsonAsync<BehaviourSettings>("/api/assessment/behaviour", Json.Options))!;

        Assert.Contains(settings.Traits, t => t.Name == "Punctual" && t.IsActive);
        Assert.Contains(settings.Traits, t => t.Name == "Works independently");
        Assert.Equal("Exceptional", settings.RatingKey[0].Meaning);
        Assert.Equal(6, settings.MaxClubs);
    }

    [Fact]
    public async Task Only_the_form_teacher_or_an_admin_sets_behaviour()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var url = $"/api/reports/{s.Students[0]}/{s.TermId}/behaviour";
        var body = new { ratings = Array.Empty<object>(), clubs = new[] { new { name = "Chess", rating = (string?)null } } };

        await (await s.SubjectTeacher.PutAsJsonAsync(url, body)).AssertProblemAsync(HttpStatusCode.Forbidden, "report.not_form_teacher");
        await (await (await Api.ClientAsync(Roles.Parent)).PutAsJsonAsync(url, body)).AssertProblemAsync(HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.OK, (await (await Api.ClientAsync(Roles.Admin)).PutAsJsonAsync(url, body)).StatusCode);
    }

    [Fact]
    public async Task Invalid_ratings_and_unknown_traits_are_refused()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var url = $"/api/reports/{s.Students[0]}/{s.TermId}/behaviour";
        var trait = (await TraitsAsync(s.FormTeacher)).First(t => t.IsActive).Id;

        var bad = await s.FormTeacher.PutAsJsonAsync(url, new { ratings = new[] { new { traitId = trait, rating = "F" } } });
        Assert.True((await bad.AssertProblemAsync(HttpStatusCode.BadRequest, "validation")).GetProperty("errors").TryGetProperty("ratings", out _));
        await (await s.FormTeacher.PutAsJsonAsync(url, new { ratings = new[] { new { traitId = Guid.NewGuid(), rating = "A" } } }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        var tooMany = Enumerable.Range(1, 7).Select(i => new { name = $"Club {i}", rating = (string?)null });
        await (await s.FormTeacher.PutAsJsonAsync(url, new { clubs = tooMany })).AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
    }

    [Fact]
    public async Task Behaviour_is_locked_once_the_report_is_published()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        await ScoreAsync(admin, s, s.SubjectId, (s.Students[0], 20, 40));
        (await admin.PutAsJsonAsync("/api/reports/attendance", new
        {
            classRoomId = s.ClassRoomId, termId = s.TermId, daysOpened = 60, entries = s.Students.Select(id => new { studentId = id, daysPresent = 58 }),
        })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync("/api/reports/publish", new { classRoomId = s.ClassRoomId, termId = s.TermId })).EnsureSuccessStatusCode();

        await (await s.FormTeacher.PutAsJsonAsync($"/api/reports/{s.Students[0]}/{s.TermId}/behaviour", new { clubs = new[] { new { name = "Chess" } } }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "report.published");
    }

    [Fact]
    public async Task Admins_edit_the_traits_and_rated_traits_are_kept_when_removed()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        var original = await TraitsAsync(admin);
        var name = "Kindness " + Guid.NewGuid().ToString("N")[..6];

        var added = await SaveTraitsAsync(admin, [.. original.Select(t => new TraitInput(t.Id, t.Name, t.IsActive)), new TraitInput(null, name, true)]);
        var trait = added.Traits.Single(t => t.Name == name);
        Assert.Equal(added.Traits.Count, trait.Order);
        (await s.FormTeacher.PutAsJsonAsync($"/api/reports/{s.Students[0]}/{s.TermId}/behaviour",
            new { ratings = new[] { new { traitId = trait.Id, rating = "C" } } })).EnsureSuccessStatusCode();

        // Removing it from the list only deactivates it: the report that rated it still shows it.
        var after = await SaveTraitsAsync(admin, [.. original.Select(t => new TraitInput(t.Id, t.Name, t.IsActive))]);
        Assert.Contains(after.Traits, t => t.Id == trait.Id && !t.IsActive && t.InUse);
        Assert.Contains((await CardAsync(admin, s.Students[0], s.TermId)).Behaviour, b => b.TraitId == trait.Id && b.Rating == "C");
        Assert.DoesNotContain((await CardAsync(admin, s.Students[1], s.TermId)).Behaviour, b => b.TraitId == trait.Id);
    }

    [Fact]
    public async Task Only_admins_change_the_trait_list()
    {
        var teacher = await Api.ClientAsync(Roles.Teacher);
        await (await teacher.PutAsJsonAsync("/api/assessment/behaviour", new { traits = Array.Empty<object>() })).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await (await Api.ClientAsync(Roles.Parent)).GetAsync("/api/assessment/behaviour")).AssertProblemAsync(HttpStatusCode.Forbidden);
        var admin = await Api.ClientAsync(Roles.Admin);
        await (await admin.PutAsJsonAsync("/api/assessment/behaviour", new { traits = new[] { new { name = "Neat", isActive = true }, new { name = "neat ", isActive = true } } }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
    }

    [Fact]
    public async Task A_full_report_with_15_subjects_traits_and_clubs_still_fits_one_a4_page()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        var subjects = new List<Guid>();
        for (var i = 0; i < 15; i++)
        {
            var code = "Z" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            var created = await admin.PostAsJsonAsync("/api/academics/subjects", new { name = $"Subject {code}", code });
            created.EnsureSuccessStatusCode();
            subjects.Add((await created.Content.ReadFromJsonAsync<SubjectDto>(Json.Options))!.Id);
        }
        (await admin.PutAsJsonAsync($"/api/academics/classrooms/{s.ClassRoomId}/subjects", new
        {
            subjects = subjects.Select(id => new { subjectId = id, teacherId = (Guid?)null }),
        })).EnsureSuccessStatusCode();
        foreach (var subject in subjects) await ScoreAsync(admin, s, subject, (s.Students[0], 22.5m, 31), (s.Students[1], 18, 27.5m));
        var traits = (await TraitsAsync(admin)).Where(t => t.IsActive);
        (await admin.PutAsJsonAsync($"/api/reports/{s.Students[0]}/{s.TermId}/behaviour", new
        {
            ratings = traits.Select((t, i) => new { traitId = t.Id, rating = "ABCDE"[i % 5].ToString() }),
            clubs = Enumerable.Range(1, 6).Select(i => new { name = $"Club number {i}", rating = (string?)"Excellent" }),
        })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/reports/{s.Students[0]}/{s.TermId}/form-teacher-comment",
            new { comment = string.Join(' ', Enumerable.Repeat("A thoughtful and hardworking pupil who helps others.", 4)) })).EnsureSuccessStatusCode();

        var response = await admin.GetAsync($"/api/reports/{s.Students[0]}/{s.TermId}/pdf");

        response.EnsureSuccessStatusCode();
        using var pdf = PdfDocument.Open(await response.Content.ReadAsByteArrayAsync());
        Assert.Equal(1, pdf.NumberOfPages);
        var text = pdf.GetPage(1).Text;
        Assert.Contains("AFFECTIVE & PSYCHOMOTOR TRAITS", text);
        Assert.Contains("Punctual", text);
        Assert.Contains("CLUB NUMBER 6", text);
        Assert.Contains("Working towards", text);
    }

    // ---------- helpers ----------

    private static async Task<IReadOnlyList<BehaviourTraitDto>> TraitsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<BehaviourSettings>("/api/assessment/behaviour", Json.Options))!.Traits;

    private static async Task<BehaviourSettings> SaveTraitsAsync(HttpClient admin, IReadOnlyList<TraitInput> traits)
    {
        var response = await admin.PutAsJsonAsync("/api/assessment/behaviour", new { traits });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BehaviourSettings>(Json.Options))!;
    }

    private static async Task ScoreAsync(HttpClient client, School s, Guid subjectId, params (Guid Student, decimal Cat, decimal Exams)[] rows)
    {
        var settings = (await client.GetFromJsonAsync<AssessmentSettings>("/api/assessment/settings", Json.Options))!;
        var cat = settings.Components.Single(c => c.Name == "CAT" && c.IsActive).Id;
        var exams = settings.Components.Single(c => c.Name == "Exams" && c.IsActive).Id;
        (await client.PutAsJsonAsync("/api/assessment/gradesheet", new
        {
            classRoomId = s.ClassRoomId, subjectId, termId = s.TermId,
            entries = rows.Select(r => new { studentId = r.Student, scores = new[] { new { componentId = cat, score = (decimal?)r.Cat }, new { componentId = exams, score = (decimal?)r.Exams } } }),
        })).EnsureSuccessStatusCode();
    }

    private static async Task<ReportCard> CardAsync(HttpClient client, Guid studentId, Guid termId)
    {
        var response = await client.GetAsync($"/api/reports/{studentId}/{termId}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReportCard>(Json.Options))!;
    }
}
