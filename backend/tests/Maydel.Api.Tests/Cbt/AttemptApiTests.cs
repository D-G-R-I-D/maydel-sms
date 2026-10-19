using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Admissions;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Academics;
using Maydel.Application.Cbt;
using Maydel.Application.Identity;
using Maydel.Application.Students;
using Maydel.Domain.Cbt;
using Maydel.Domain.Common;

namespace Maydel.Api.Tests.Cbt;

/// <summary>E16.3 / E16.6: a student sits a published test: start, autosave, one live session, focus log, deadline, submit.</summary>
[Collection(nameof(ApiCollection))]
public sealed class AttemptApiTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task A_student_sits_the_test_with_autosave_and_the_server_clock_deciding()
    {
        var (s, test, questions) = await PublishedTestAsync();
        var student = await StudentClientAsync(s.Students[0]);

        var mine = await AdmissionsKit.ReadAsync<List<StudentTestDto>>(await student.GetAsync("/api/student/tests"));
        var listed = Assert.Single(mine, x => x.Id == test.Id);
        Assert.Equal((StudentTestState.Open, 0), (listed.State, listed.AttemptsUsed));

        var paper = await AdmissionsKit.ReadAsync<AttemptPaper>(await student.PostAsync($"/api/student/tests/{test.Id}/start", null));
        Assert.Equal((AttemptStatus.InProgress, 3), (paper.Status, paper.Items.Count));
        Assert.Equal(45 * 60, paper.RemainingSeconds);
        Assert.NotNull(paper.SessionToken);
        // No answer keys on the student's paper.
        var json = await (await student.GetAsync($"/api/student/attempts/{paper.Id}")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("isCorrect", json);
        Assert.DoesNotContain("markingGuide", json);

        var objective = paper.Items.First(i => i.Kind == QuestionKind.Objective);
        var right = questions[objective.Text].Options.Single(o => o.IsCorrect).Id;
        var saved = await AdmissionsKit.ReadAsync<AnswerSaved>(await student.PutAsJsonAsync($"/api/student/attempts/{paper.Id}/items/{objective.Id}",
            new { sessionToken = paper.SessionToken, optionIds = new[] { right } }));
        Assert.True(saved.RemainingSeconds > 0);
        var gap = paper.Items.Single(i => i.Kind == QuestionKind.FillInTheGap);
        (await student.PutAsJsonAsync($"/api/student/attempts/{paper.Id}/items/{gap.Id}", new { sessionToken = paper.SessionToken, text = " abuja " })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await student.PostAsJsonAsync($"/api/student/attempts/{paper.Id}/events", new { sessionToken = paper.SessionToken, kind = "Blur" })).StatusCode);

        // Reopening (another tab/device) resumes the same attempt and the old token stops working.
        var resumed = await AdmissionsKit.ReadAsync<AttemptPaper>(await student.PostAsync($"/api/student/tests/{test.Id}/start", null));
        Assert.Equal((paper.Id, 2), (resumed.Id, resumed.Answered));
        await (await student.PutAsJsonAsync($"/api/student/attempts/{paper.Id}/items/{gap.Id}", new { sessionToken = paper.SessionToken, text = "Lagos" }))
            .AssertProblemAsync(HttpStatusCode.Conflict, "attempt.other_session");

        // Other students can't touch it; parents can't sit tests.
        var classmate = await StudentClientAsync(s.Students[1]);
        await (await classmate.GetAsync($"/api/student/attempts/{paper.Id}")).AssertProblemAsync(HttpStatusCode.NotFound);
        await (await (await Api.ClientAsync(Roles.Parent)).GetAsync("/api/student/tests")).AssertProblemAsync(HttpStatusCode.Forbidden);

        var handedIn = await AdmissionsKit.ReadAsync<AttemptPaper>(await student.PostAsJsonAsync($"/api/student/attempts/{paper.Id}/submit", new { sessionToken = resumed.SessionToken }));
        Assert.Equal((AttemptStatus.Submitted, false, (string?)null), (handedIn.Status, handedIn.AutoSubmitted, handedIn.SessionToken));
        await (await student.PutAsJsonAsync($"/api/student/attempts/{paper.Id}/items/{gap.Id}", new { sessionToken = resumed.SessionToken, text = "x" }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "attempt.submitted");
        await (await student.PostAsync($"/api/student/tests/{test.Id}/start", null)).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "test.no_attempts");
        Assert.Equal(StudentTestState.Done, (await AdmissionsKit.ReadAsync<List<StudentTestDto>>(await student.GetAsync("/api/student/tests"))).Single(x => x.Id == test.Id).State);

        // Once a student has started, the teacher can't pull the test back to draft.
        await (await s.SubjectTeacher.PostAsync($"/api/cbt/tests/{test.Id}/unpublish", null)).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "test.started");
    }

    [Fact]
    public async Task Time_up_hands_the_paper_in_and_later_saves_are_refused()
    {
        var (s, test, _) = await PublishedTestAsync();
        var student = await StudentClientAsync(s.Students[2]);
        var paper = await AdmissionsKit.ReadAsync<AttemptPaper>(await student.PostAsync($"/api/student/tests/{test.Id}/start", null));
        var item = paper.Items[0];

        var original = Api.Clock.Now;
        try
        {
            Api.Clock.Now = original.AddMinutes(46);
            await (await student.PutAsJsonAsync($"/api/student/attempts/{paper.Id}/items/{item.Id}", new { sessionToken = paper.SessionToken, text = "late" }))
                .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "attempt.submitted");
            var closed = await AdmissionsKit.ReadAsync<AttemptPaper>(await student.GetAsync($"/api/student/attempts/{paper.Id}"));
            Assert.Equal((AttemptStatus.Submitted, true, 0), (closed.Status, closed.AutoSubmitted, closed.RemainingSeconds));
            Assert.Equal(paper.DeadlineAt, closed.SubmittedAt);
        }
        finally
        {
            Api.Clock.Now = original;
        }
    }

    /// <summary>A published 3-question test (objective 2 marks, gap 1 mark, theory 5 marks) for the school's class, open now on the fake clock.</summary>
    private async Task<(School School, TestDetail Test, Dictionary<string, QuestionDto> Questions)> PublishedTestAsync()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        var level = (await admin.GetFromJsonAsync<List<ClassRoomDto>>("/api/academics/classrooms", Json.Options))!.Single(r => r.Id == s.ClassRoomId).ClassLevelId;
        var t = s.SubjectTeacher;
        var tag = Guid.NewGuid().ToString("N")[..6];
        var objective = await AdmissionsKit.ReadAsync<QuestionDto>(await t.PostAsJsonAsync("/api/cbt/questions", new
        {
            subjectId = s.SubjectId, classLevelId = level, kind = "Objective", text = $"Which is even? {tag}",
            options = new[] { new { text = "3", isCorrect = false }, new { text = "4", isCorrect = true }, new { text = "5", isCorrect = false } },
        }));
        var gap = await AdmissionsKit.ReadAsync<QuestionDto>(await t.PostAsJsonAsync("/api/cbt/questions", new
        {
            subjectId = s.SubjectId, classLevelId = level, kind = "FillInTheGap", text = $"The capital of Nigeria is ___. {tag}", acceptedAnswers = new[] { "Abuja" },
        }));
        var theory = await AdmissionsKit.ReadAsync<QuestionDto>(await t.PostAsJsonAsync("/api/cbt/questions", new
        {
            subjectId = s.SubjectId, classLevelId = level, kind = "Theory", text = $"Describe the water cycle. {tag}", markingGuide = "Evaporation, condensation, precipitation.",
        }));
        // Open from 07:30 to 10:30 in Abuja on the fake clock's day (it reads 08:00).
        var test = await AdmissionsKit.ReadAsync<TestDetail>(await t.PostAsJsonAsync("/api/cbt/tests", new
        {
            title = $"CAT {tag}", subjectId = s.SubjectId, classLevelId = level, termId = s.TermId, classRoomIds = new[] { s.ClassRoomId }, durationMinutes = 45,
            opensAt = "2026-10-15T07:30:00+01:00", closesAt = "2026-10-15T10:30:00+01:00",
        }));
        (await t.PutAsJsonAsync($"/api/cbt/tests/{test.Id}/sections", new
        {
            sections = new object[]
            {
                new { title = "Objective", kind = "Objective", marksPerQuestion = 2, mode = "Fixed", questionIds = new[] { objective.Id } },
                new { title = "Gap", kind = "FillInTheGap", marksPerQuestion = 1, mode = "Fixed", questionIds = new[] { gap.Id } },
                new { title = "Theory", kind = "Theory", marksPerQuestion = 5, mode = "Fixed", questionIds = new[] { theory.Id } },
            },
        })).EnsureSuccessStatusCode();
        var published = await AdmissionsKit.ReadAsync<TestDetail>(await t.PostAsync($"/api/cbt/tests/{test.Id}/publish", null));
        return (s, published, new[] { objective, gap, theory }.ToDictionary(q => q.Text));
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
