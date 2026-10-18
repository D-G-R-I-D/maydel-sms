using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Admissions;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Academics;
using Maydel.Application.Assessment;
using Maydel.Application.Cbt;
using Maydel.Domain.Cbt;
using Maydel.Domain.Common;

namespace Maydel.Api.Tests.Cbt;

/// <summary>E16.2 / E16.2a: tests built from sections, scaled to a gradebook component, checked before publishing.</summary>
[Collection(nameof(ApiCollection))]
public sealed class CbtTestTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task A_teacher_builds_a_mixed_test_sees_the_scaling_and_publishes_once_it_is_ready()
    {
        var (s, level, component) = await SetUpAsync();
        var t = s.SubjectTeacher;
        var topic = "Algebra " + Guid.NewGuid().ToString("N")[..6];
        var objectives = new List<Guid>();
        for (var i = 0; i < 4; i++) objectives.Add((await ObjectiveAsync(t, s.SubjectId, level, topic)).Id);
        var theory = await AdmissionsKit.ReadAsync<QuestionDto>(await t.PostAsJsonAsync("/api/cbt/questions", new
        {
            subjectId = s.SubjectId, classLevelId = level, kind = "Theory", text = "Solve 2x + 3 = 11, showing working.", markingGuide = "x = 4 (2 marks for working)", topic,
        }));

        var draft = await AdmissionsKit.ReadAsync<TestDetail>(await t.PostAsJsonAsync("/api/cbt/tests", Header(s, level, component.Id)));
        Assert.Equal((TestStatus.Draft, true), (draft.Status, draft.CanEdit));
        Assert.Contains("Add at least one section.", draft.Problems);

        // Two fixed objective questions at 2 marks, two drawn at random from the topic, one theory question at 10 marks: 4 + 2 + 10 = 16.
        var built = await AdmissionsKit.ReadAsync<TestDetail>(await t.PutAsJsonAsync($"/api/cbt/tests/{draft.Id}/sections", new
        {
            sections = new object[]
            {
                new { title = "Objective", kind = "Objective", marksPerQuestion = 2, mode = "Fixed", questionIds = objectives.Take(2) },
                new { title = "Quick fire", kind = "Objective", marksPerQuestion = 1, mode = "Random", randomCount = 2, topic },
                new { title = "Theory", instructions = "Answer in full.", kind = "Theory", marksPerQuestion = 10, mode = "Fixed", questionIds = new[] { theory.Id } },
            },
        }));
        Assert.Equal((5, 16m), (built.QuestionCount, built.TotalMarks));
        Assert.Equal(new[] { 4m, 2m, 10m }, built.Sections.Select(x => x.Marks));
        Assert.True(built.Sections[1].AvailableInBank >= 4);
        Assert.Equal((component.MaxScore, 16m), (built.Scaling!.ComponentMax, built.Scaling.TotalMarks));
        Assert.Empty(built.Problems);

        // Ask for more random questions than the bank has: not ready.
        var greedy = await AdmissionsKit.ReadAsync<TestDetail>(await t.PutAsJsonAsync($"/api/cbt/tests/{draft.Id}/sections", new
        {
            sections = new object[] { new { title = "Too many", kind = "Objective", marksPerQuestion = 1, mode = "Random", randomCount = 50, topic } },
        }));
        Assert.Contains(greedy.Problems, p => p.Contains("only has"));
        await (await t.PostAsync($"/api/cbt/tests/{draft.Id}/publish", null)).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "test.not_ready");

        // Wrong kind in a fixed section is refused outright.
        await (await t.PutAsJsonAsync($"/api/cbt/tests/{draft.Id}/sections", new
        {
            sections = new object[] { new { title = "Objective", kind = "Objective", marksPerQuestion = 1, mode = "Fixed", questionIds = new[] { theory.Id } } },
        })).AssertProblemAsync(HttpStatusCode.BadRequest, "validation");

        (await t.PutAsJsonAsync($"/api/cbt/tests/{draft.Id}/sections", new
        {
            sections = new object[] { new { title = "Objective", kind = "Objective", marksPerQuestion = 2, mode = "Fixed", questionIds = objectives } },
        })).EnsureSuccessStatusCode();
        var published = await AdmissionsKit.ReadAsync<TestDetail>(await t.PostAsync($"/api/cbt/tests/{draft.Id}/publish", null));
        Assert.Equal((TestStatus.Published, false), (published.Status, published.CanEdit));
        await (await t.PutAsJsonAsync($"/api/cbt/tests/{draft.Id}", Header(s, level, component.Id))).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "test.locked");
        await (await t.DeleteAsync($"/api/cbt/tests/{draft.Id}")).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "test.locked");

        var list = await AdmissionsKit.ReadAsync<List<TestSummary>>(await s.FormTeacher.GetAsync($"/api/cbt/tests?termId={s.TermId}&subjectId={s.SubjectId}"));
        Assert.Contains(list, x => x.Id == draft.Id && x.QuestionCount == 4 && x.ClassRooms.Count == 1);

        Assert.Equal(TestStatus.Draft, (await AdmissionsKit.ReadAsync<TestDetail>(await t.PostAsync($"/api/cbt/tests/{draft.Id}/unpublish", null))).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await t.DeleteAsync($"/api/cbt/tests/{draft.Id}")).StatusCode);
    }

    [Fact]
    public async Task Teachers_only_set_tests_for_their_own_subjects_and_classes()
    {
        var (s, level, component) = await SetUpAsync();
        var (outsider, _) = await SchoolBuilder.TeacherAsync(Api);
        await (await outsider.PostAsJsonAsync("/api/cbt/tests", Header(s, level, component.Id))).AssertProblemAsync(HttpStatusCode.Forbidden);

        var mine = await AdmissionsKit.ReadAsync<TestDetail>(await s.SubjectTeacher.PostAsJsonAsync("/api/cbt/tests", Header(s, level, component.Id)));
        await (await outsider.GetAsync($"/api/cbt/tests/{mine.Id}")).AssertProblemAsync(HttpStatusCode.NotFound);
        Assert.DoesNotContain(await AdmissionsKit.ReadAsync<List<TestSummary>>(await outsider.GetAsync($"/api/cbt/tests?termId={s.TermId}")), x => x.Id == mine.Id);

        // Another level's arm isn't allowed; a window shorter than the sitting isn't either.
        var admin = await Api.ClientAsync(Roles.Admin);
        var otherRoom = (await admin.GetFromJsonAsync<List<ClassRoomDto>>("/api/academics/classrooms", Json.Options))!.First(r => r.ClassLevelId != level);
        await (await s.SubjectTeacher.PostAsJsonAsync("/api/cbt/tests", Header(s, level, component.Id, rooms: [otherRoom.Id])))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        await (await s.SubjectTeacher.PostAsJsonAsync("/api/cbt/tests", Header(s, level, component.Id, closesAfterMinutes: 30)))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
    }

    private async Task<(School School, Guid Level, ComponentDto Component)> SetUpAsync()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        var level = (await admin.GetFromJsonAsync<List<ClassRoomDto>>("/api/academics/classrooms", Json.Options))!.Single(r => r.Id == s.ClassRoomId).ClassLevelId;
        var component = (await admin.GetFromJsonAsync<AssessmentSettings>("/api/assessment/settings", Json.Options))!.Components.First(c => c.IsActive);
        return (s, level, component);
    }

    private static object Header(School s, Guid level, Guid component, Guid[]? rooms = null, int closesAfterMinutes = 180) => new
    {
        title = "Week 6 CAT", subjectId = s.SubjectId, classLevelId = level, termId = s.TermId, componentId = component,
        classRoomIds = rooms ?? [s.ClassRoomId], durationMinutes = 45,
        opensAt = "2026-10-20T09:00:00+01:00", closesAt = new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.FromHours(1)).AddMinutes(closesAfterMinutes),
    };

    private static async Task<QuestionDto> ObjectiveAsync(HttpClient t, Guid subject, Guid level, string topic) =>
        await AdmissionsKit.ReadAsync<QuestionDto>(await t.PostAsJsonAsync("/api/cbt/questions", new
        {
            subjectId = subject, classLevelId = level, kind = "Objective", text = $"Q{Guid.NewGuid():N}", topic,
            options = new[] { new { text = "Yes", isCorrect = true }, new { text = "No", isCorrect = false } },
        }));
}
