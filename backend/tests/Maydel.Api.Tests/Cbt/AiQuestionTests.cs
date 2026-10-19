using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Maydel.Api.Tests.Admissions;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Academics;
using Maydel.Application.Cbt;
using Maydel.Domain.Cbt;
using Maydel.Domain.Common;

namespace Maydel.Api.Tests.Cbt;

/// <summary>E16.8 / E16.9: AI-written draft questions from the teacher's material, regeneration limits, saving to the bank, quotas.</summary>
[Collection(nameof(ApiCollection))]
public sealed class AiQuestionTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task A_teacher_generates_regenerates_up_to_five_times_and_saves_edited_drafts()
    {
        var (s, level) = await SchoolAsync();
        var t = s.SubjectTeacher;
        var before = (await AdmissionsKit.ReadAsync<AiQuotaDto>(await t.GetAsync("/api/cbt/ai/quota"))).Used;

        var g = await AdmissionsKit.ReadAsync<AiGenerationDto>(await t.PostAsync("/api/cbt/ai/generations",
            Form(s.SubjectId, level, "Objective,TrueFalse,Theory,ShortAnswer", count: 4, easy: 2, medium: 1, hard: 1, pdf: true)));
        var v1 = Assert.Single(g.Versions);
        Assert.Equal((GenerationStatus.Ready, 4), (v1.Status, v1.Questions.Count));
        Assert.Contains(v1.Questions, q => q.Question.Kind == QuestionKind.ShortAnswer && q.Problem is null); // keys given: fine
        Assert.Equal(["notes.pdf"], g.Files);
        Assert.Equal(before + 1, g.Quota.Used);

        // Only scope and the teacher's material go to the AI.
        var sent = Api.Ai.Requests.Last();
        Assert.Equal((4, 2, 1, 1), (sent.Count, sent.Easy, sent.Medium, sent.Hard));
        Assert.Equal("application/pdf", Assert.Single(sent.Files).ContentType);
        Assert.Empty(sent.Avoid);

        for (var i = 0; i < AiGeneration.MaxRegenerations; i++)
            g = await AdmissionsKit.ReadAsync<AiGenerationDto>(await t.PostAsync($"/api/cbt/ai/generations/{g.Id}/regenerate", null));
        Assert.Equal((6, 5, false), (g.Versions.Count, g.Regenerations, g.CanRegenerate));
        Assert.NotEmpty(Api.Ai.Requests.Last().Avoid); // regenerations steer away from earlier versions
        await (await t.PostAsync($"/api/cbt/ai/generations/{g.Id}/regenerate", null)).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "ai.regenerations");

        // Accept version 3, edit freely, save to the bank as AI-written.
        var v3 = g.Versions.Single(v => v.Number == 3).Questions;
        var objective = v3.First(q => q.Question.Kind == QuestionKind.Objective).Question;
        var theory = v3.First(q => q.Question.Kind == QuestionKind.Theory).Question;
        var saved = await AdmissionsKit.ReadAsync<List<QuestionDto>>(await t.PostAsJsonAsync($"/api/cbt/ai/generations/{g.Id}/save", new
        {
            questions = new object[]
            {
                new { kind = "Objective", text = objective.Text + " (edited)", options = objective.Options.Select(o => new { text = o.Text, isCorrect = o.IsCorrect }), topic = objective.Topic, difficulty = "Easy" },
                new { kind = "Theory", text = theory.Text, markingGuide = theory.MarkingGuide, topic = theory.Topic, difficulty = "Hard" },
            },
        }));
        Assert.Equal(2, saved.Count);
        Assert.All(saved, q => Assert.Equal((QuestionSource.Ai, s.SubjectId, level), (q.Source, q.SubjectId, q.ClassLevelId)));
        Assert.EndsWith("(edited)", saved[0].Text);

        // A draft that breaks the bank's rules is refused, with the question's position.
        await (await t.PostAsJsonAsync($"/api/cbt/ai/generations/{g.Id}/save", new
        {
            questions = new object[] { new { kind = "Theory", text = "Explain.", difficulty = "Hard" } },
        })).AssertProblemAsync(HttpStatusCode.BadRequest, "validation");

        // Other teachers can't see it or write for this subject.
        var (outsider, _) = await SchoolBuilder.TeacherAsync(Api);
        await (await outsider.GetAsync($"/api/cbt/ai/generations/{g.Id}")).AssertProblemAsync(HttpStatusCode.NotFound);
        await (await outsider.PostAsync("/api/cbt/ai/generations", Form(s.SubjectId, level, "Objective", 1, 1, 0, 0, pdf: false)))
            .AssertProblemAsync(HttpStatusCode.Forbidden);
        Assert.Contains(await AdmissionsKit.ReadAsync<List<AiGenerationSummary>>(await t.GetAsync("/api/cbt/ai/generations")), x => x.Id == g.Id && x.Versions == 6);
    }

    [Fact]
    public async Task Allowances_are_per_teacher_failures_are_free_and_admins_see_usage()
    {
        var (s, level) = await SchoolAsync();
        var t = s.FormTeacher;
        var admin = await Api.ClientAsync(Roles.Admin);
        var me = await AdmissionsKit.ReadAsync<Maydel.Application.Identity.MeResponse>(await t.GetAsync("/api/auth/me"));

        // Teachers appear on the dashboard before they use anything, so allowances can be set up front.
        var fresh = Assert.Single((await AdmissionsKit.ReadAsync<AiUsage>(await admin.GetAsync("/api/cbt/ai/usage"))).Teachers, r => r.UserId == me.Id);
        Assert.Equal((0, false), (fresh.Used, fresh.CustomAllowance));

        var first = await AdmissionsKit.ReadAsync<AiGenerationDto>(await t.PostAsync("/api/cbt/ai/generations", Form(s.SubjectId, level, "TrueFalse", 2, 2, 0, 0, pdf: false)));
        var row = Assert.Single((await AdmissionsKit.ReadAsync<AiUsage>(await admin.GetAsync("/api/cbt/ai/usage"))).Teachers, r => r.UserId == me.Id);
        Assert.Equal((1, 1200L, 800L), (row.Used, row.InputTokens, row.OutputTokens));
        var capped = await AdmissionsKit.ReadAsync<AiUsageRow>(await admin.PutAsJsonAsync($"/api/cbt/ai/allowances/{me.Id}", new { monthlyGenerations = 2 }));
        Assert.Equal((2, true, 1), (capped.Allowance, capped.CustomAllowance, capped.Used));

        // A failed generation (e.g. a refusal) is recorded but doesn't use the allowance.
        Api.Ai.FailNextWith = "The AI declined to write questions from this material.";
        var failed = await AdmissionsKit.ReadAsync<AiGenerationDto>(await t.PostAsync($"/api/cbt/ai/generations/{first.Id}/regenerate", null));
        Assert.Equal((GenerationStatus.Failed, 1), (failed.Versions[^1].Status, failed.Quota.Used));

        (await t.PostAsync($"/api/cbt/ai/generations/{first.Id}/regenerate", null)).EnsureSuccessStatusCode();
        await (await t.PostAsync($"/api/cbt/ai/generations/{first.Id}/regenerate", null)).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "ai.quota");
        // Someone else's allowance is untouched.
        Assert.True((await AdmissionsKit.ReadAsync<AiQuotaDto>(await s.SubjectTeacher.GetAsync("/api/cbt/ai/quota"))).Remaining > 0);

        // Admin can switch it off school-wide; the server without a key reports it unavailable.
        var settings = await AdmissionsKit.ReadAsync<AiSettingsDto>(await admin.GetAsync("/api/cbt/ai/settings"));
        try
        {
            (await admin.PutAsJsonAsync("/api/cbt/ai/settings", new { enabled = false, defaultMonthlyGenerations = settings.DefaultMonthlyGenerations, schoolMonthlyGenerations = settings.SchoolMonthlyGenerations })).EnsureSuccessStatusCode();
            await (await s.SubjectTeacher.PostAsync("/api/cbt/ai/generations", Form(s.SubjectId, level, "Objective", 1, 1, 0, 0, pdf: false)))
                .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "ai.disabled");
        }
        finally
        {
            (await admin.PutAsJsonAsync("/api/cbt/ai/settings", new { enabled = true, defaultMonthlyGenerations = settings.DefaultMonthlyGenerations, schoolMonthlyGenerations = settings.SchoolMonthlyGenerations })).EnsureSuccessStatusCode();
        }
        Api.Ai.IsEnabled = false;
        try
        {
            await (await s.SubjectTeacher.PostAsync("/api/cbt/ai/generations", Form(s.SubjectId, level, "Objective", 1, 1, 0, 0, pdf: false)))
                .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "ai.unavailable");
        }
        finally
        {
            Api.Ai.IsEnabled = true;
        }
        await (await t.GetAsync("/api/cbt/ai/usage")).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    private async Task<(School School, Guid Level)> SchoolAsync()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        var level = (await admin.GetFromJsonAsync<List<ClassRoomDto>>("/api/academics/classrooms", Json.Options))!.Single(r => r.Id == s.ClassRoomId).ClassLevelId;
        return (s, level);
    }

    private static MultipartFormDataContent Form(Guid subject, Guid level, string kinds, int count, int easy, int medium, int hard, bool pdf)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(subject.ToString()), "subjectId" }, { new StringContent(level.ToString()), "classLevelId" },
            { new StringContent("Cat"), "assessmentType" }, { new StringContent(kinds), "kinds" }, { new StringContent(count.ToString()), "count" },
            { new StringContent(easy.ToString()), "easy" }, { new StringContent(medium.ToString()), "medium" }, { new StringContent(hard.ToString()), "hard" },
            { new StringContent("Fractions: adding unlike denominators"), "guidance" },
        };
        if (pdf)
        {
            var file = new ByteArrayContent("%PDF-1.4\n%notes\n"u8.ToArray());
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(file, "files", "notes.pdf");
        }
        return form;
    }
}
