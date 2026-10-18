using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Maydel.Api.Tests.Admissions;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Academics;
using Maydel.Application.Cbt;
using Maydel.Application.Common;
using Maydel.Domain.Cbt;
using Maydel.Domain.Common;

namespace Maydel.Api.Tests.Cbt;

/// <summary>E16.1: the shared question bank per subject and class level, with row-level access for teachers.</summary>
[Collection(nameof(ApiCollection))]
public sealed class QuestionBankTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task Teachers_of_a_subject_share_its_bank_and_others_cannot_see_it()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var level = await LevelOfAsync(s);
        var topic = "Fractions " + Guid.NewGuid().ToString("N")[..6];

        var created = await AdmissionsKit.ReadAsync<QuestionDto>(await s.SubjectTeacher.PostAsJsonAsync("/api/cbt/questions", new
        {
            subjectId = s.SubjectId, classLevelId = level, kind = "Objective", text = "What is 1/2 + 1/4?", topic, difficulty = "Easy",
            options = new[] { new { text = "3/4", isCorrect = true }, new { text = "2/6", isCorrect = false }, new { text = "1/8", isCorrect = false } },
        }));
        Assert.Equal((QuestionKind.Objective, 3, true, QuestionSource.Manual), (created.Kind, created.Options.Count, created.IsAutoMarked, created.Source));
        Assert.NotNull(created.CreatedBy);

        // The form teacher of an arm at that level shares the bank, for any subject.
        var page = await AdmissionsKit.ReadAsync<PagedResult<QuestionDto>>(
            await s.FormTeacher.GetAsync($"/api/cbt/questions?subjectId={s.SubjectId}&classLevelId={level}&topic={Uri.EscapeDataString(topic)}"));
        Assert.Equal(created.Id, Assert.Single(page.Items).Id);
        Assert.Contains(topic, await AdmissionsKit.ReadAsync<List<string>>(await s.FormTeacher.GetAsync($"/api/cbt/topics?subjectId={s.SubjectId}&classLevelId={level}")));
        var scopes = await AdmissionsKit.ReadAsync<List<AuthoringScope>>(await s.SubjectTeacher.GetAsync("/api/cbt/scopes"));
        Assert.Contains(scopes, x => x.SubjectId == s.SubjectId && x.ClassLevelId == level && x.Questions >= 1);
        Assert.DoesNotContain(scopes, x => x.SubjectId == s.OtherSubjectId);

        // A teacher who doesn't teach it: can't list, read, write or archive.
        var (outsider, _) = await SchoolBuilder.TeacherAsync(Api);
        await (await outsider.GetAsync($"/api/cbt/questions?subjectId={s.SubjectId}&classLevelId={level}")).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await outsider.GetAsync($"/api/cbt/questions/{created.Id}")).AssertProblemAsync(HttpStatusCode.NotFound);
        await (await outsider.PostAsJsonAsync("/api/cbt/questions", TrueFalse(s.SubjectId, level))).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await outsider.DeleteAsync($"/api/cbt/questions/{created.Id}")).AssertProblemAsync(HttpStatusCode.NotFound);
        Assert.Empty(await AdmissionsKit.ReadAsync<List<AuthoringScope>>(await outsider.GetAsync("/api/cbt/scopes")));
        // The subject teacher can't write for the other subject either; parents and bursars have no access at all.
        await (await s.SubjectTeacher.PostAsJsonAsync("/api/cbt/questions", TrueFalse(s.OtherSubjectId, level))).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await (await Api.ClientAsync(Roles.Bursar)).GetAsync("/api/cbt/scopes")).AssertProblemAsync(HttpStatusCode.Forbidden);

        // Archive: gone from the bank.
        Assert.Equal(HttpStatusCode.NoContent, (await s.FormTeacher.DeleteAsync($"/api/cbt/questions/{created.Id}")).StatusCode);
        Assert.Empty((await AdmissionsKit.ReadAsync<PagedResult<QuestionDto>>(
            await s.FormTeacher.GetAsync($"/api/cbt/questions?subjectId={s.SubjectId}&classLevelId={level}&topic={Uri.EscapeDataString(topic)}"))).Items);
    }

    [Fact]
    public async Task Every_kind_is_checked_and_edits_keep_option_identity()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var level = await LevelOfAsync(s);
        var t = s.SubjectTeacher;

        await (await t.PostAsJsonAsync("/api/cbt/questions", new
        {
            subjectId = s.SubjectId, classLevelId = level, kind = "Objective", text = "Pick one",
            options = new[] { new { text = "A", isCorrect = true }, new { text = "B", isCorrect = true } },
        })).AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        await (await t.PostAsJsonAsync("/api/cbt/questions", new { subjectId = s.SubjectId, classLevelId = level, kind = "Theory", text = "Explain photosynthesis." }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        await (await t.PostAsJsonAsync("/api/cbt/questions", new { subjectId = s.SubjectId, classLevelId = level, kind = "FillInTheGap", text = "No gap here", acceptedAnswers = new[] { "x" } }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation");

        var gap = await AdmissionsKit.ReadAsync<QuestionDto>(await t.PostAsJsonAsync("/api/cbt/questions", new
        {
            subjectId = s.SubjectId, classLevelId = level, kind = "FillInTheGap", text = "The capital of Nigeria is ___.", acceptedAnswers = new[] { "Abuja", "abuja " },
        }));
        Assert.Equal(["Abuja"], gap.AcceptedAnswers);
        var theory = await AdmissionsKit.ReadAsync<QuestionDto>(await t.PostAsJsonAsync("/api/cbt/questions", new
        {
            subjectId = s.SubjectId, classLevelId = level, kind = "Theory", text = "Explain photosynthesis.", markingGuide = "Light, water, CO2 → glucose + O2 (4 marks).", difficulty = "Hard",
        }));
        Assert.False(theory.IsAutoMarked);
        Assert.False((await AdmissionsKit.ReadAsync<QuestionDto>(await t.PostAsJsonAsync("/api/cbt/questions", new
        {
            subjectId = s.SubjectId, classLevelId = level, kind = "ShortAnswer", text = "Name a noun.",
        }))).IsAutoMarked);

        var multi = await AdmissionsKit.ReadAsync<QuestionDto>(await t.PostAsJsonAsync("/api/cbt/questions", new
        {
            subjectId = s.SubjectId, classLevelId = level, kind = "MultiSelect", text = "Which are prime?",
            options = new[] { new { text = "2", isCorrect = true }, new { text = "4", isCorrect = false }, new { text = "5", isCorrect = true } },
        }));
        var keep = multi.Options[0];
        var edited = await AdmissionsKit.ReadAsync<QuestionDto>(await t.PutAsJsonAsync($"/api/cbt/questions/{multi.Id}", new
        {
            subjectId = s.SubjectId, classLevelId = level, kind = "MultiSelect", text = "Which are prime numbers?",
            options = new object[] { new { id = keep.Id, text = "2", isCorrect = true }, new { text = "7", isCorrect = true }, new { text = "9", isCorrect = false } },
        }));
        Assert.Equal(keep.Id, edited.Options[0].Id);
        Assert.Equal(["2", "7", "9"], edited.Options.Select(o => o.Text));
        Assert.NotNull(edited.UpdatedAt);

        // A picture: stored privately, served only to staff who can see the question.
        var form = new MultipartFormDataContent();
        var image = new ByteArrayContent(AdmissionsKit.Jpeg());
        image.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(image, "file", "diagram.jpg");
        Assert.True((await AdmissionsKit.ReadAsync<QuestionDto>(await t.PutAsync($"/api/cbt/questions/{theory.Id}/image", form))).HasImage);
        var file = await t.GetAsync($"/api/cbt/questions/{theory.Id}/image");
        Assert.Equal("image/png", file.Content.Headers.ContentType?.MediaType);
        var (outsider, _) = await SchoolBuilder.TeacherAsync(Api);
        await (await outsider.GetAsync($"/api/cbt/questions/{theory.Id}/image")).AssertProblemAsync(HttpStatusCode.NotFound);
        Assert.False((await AdmissionsKit.ReadAsync<QuestionDto>(await t.DeleteAsync($"/api/cbt/questions/{theory.Id}/image"))).HasImage);

        var hard = await AdmissionsKit.ReadAsync<PagedResult<QuestionDto>>(
            await t.GetAsync($"/api/cbt/questions?subjectId={s.SubjectId}&classLevelId={level}&difficulty=Hard&search=photosynthesis"));
        Assert.Contains(hard.Items, q => q.Id == theory.Id);
        Assert.All(hard.Items, q => Assert.Equal(Difficulty.Hard, q.Difficulty));
    }

    private static object TrueFalse(Guid subject, Guid level) =>
        new { subjectId = subject, classLevelId = level, kind = "TrueFalse", text = "The sun is a star.", trueFalseAnswer = true };

    private async Task<Guid> LevelOfAsync(School s)
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        return (await admin.GetFromJsonAsync<List<ClassRoomDto>>("/api/academics/classrooms", Json.Options))!.Single(r => r.Id == s.ClassRoomId).ClassLevelId;
    }
}
