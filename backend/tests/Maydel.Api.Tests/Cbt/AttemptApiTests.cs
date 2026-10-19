using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Admissions;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Academics;
using Maydel.Application.Assessment;
using Maydel.Application.Cbt;
using Maydel.Application.Identity;
using Maydel.Application.Students;
using Maydel.Domain.Cbt;
using Maydel.Domain.Common;
using Maydel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

    [Fact]
    public async Task Teachers_mark_release_and_send_scaled_scores_to_the_grade_sheet()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var component = (await admin.GetFromJsonAsync<AssessmentSettings>("/api/assessment/settings", Json.Options))!.Components.First(c => c.IsActive);
        var (s, test, questions) = await PublishedTestAsync(component.Id);
        var teacher = s.SubjectTeacher;

        // Student 0 answers everything; student 1 starts and walks away; student 2 never starts.
        var first = await StudentClientAsync(s.Students[0]);
        var paper = await AdmissionsKit.ReadAsync<AttemptPaper>(await first.PostAsync($"/api/student/tests/{test.Id}/start", null));
        foreach (var item in paper.Items)
        {
            var q = questions[item.Text];
            object body = q.Kind switch
            {
                QuestionKind.Objective => new { sessionToken = paper.SessionToken, optionIds = new[] { q.Options.Single(o => o.IsCorrect).Id } },
                QuestionKind.FillInTheGap => new { sessionToken = paper.SessionToken, text = "Abuja" },
                _ => new { sessionToken = paper.SessionToken, text = "Water evaporates, condenses into clouds and falls as rain." },
            };
            (await first.PutAsJsonAsync($"/api/student/attempts/{paper.Id}/items/{item.Id}", body)).EnsureSuccessStatusCode();
        }
        (await first.PostAsJsonAsync($"/api/student/attempts/{paper.Id}/submit", new { sessionToken = paper.SessionToken })).EnsureSuccessStatusCode();
        var second = await StudentClientAsync(s.Students[1]);
        (await second.PostAsync($"/api/student/tests/{test.Id}/start", null)).EnsureSuccessStatusCode();

        var rows = await AdmissionsKit.ReadAsync<AttemptRows>(await teacher.GetAsync($"/api/cbt/tests/{test.Id}/attempts"));
        Assert.Equal((1, 1), (rows.Sitting, rows.NeedsMarking));
        Assert.Equal(AttemptRowState.NotStarted, rows.Students.Single(r => r.StudentId == s.Students[2]).State);
        Assert.Equal(3m, rows.Students.Single(r => r.StudentId == s.Students[0]).AutoScore);
        await (await teacher.PostAsync($"/api/cbt/tests/{test.Id}/release", null)).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "test.still_open");

        var original = Api.Clock.Now;
        try
        {
            Api.Clock.Now = new DateTimeOffset(2026, 10, 15, 9, 31, 0, TimeSpan.Zero); // the window closed at 10:30 in Abuja
            rows = await AdmissionsKit.ReadAsync<AttemptRows>(await teacher.GetAsync($"/api/cbt/tests/{test.Id}/attempts"));
            Assert.Equal((0, 2), (rows.Sitting, rows.NeedsMarking)); // the walk-away was handed in automatically
            await (await teacher.PostAsync($"/api/cbt/tests/{test.Id}/release", null)).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "test.unmarked");

            // Mark theory question by question.
            var theory = questions.Values.Single(q => q.Kind == QuestionKind.Theory);
            var responses = await AdmissionsKit.ReadAsync<QuestionResponses>(await teacher.GetAsync($"/api/cbt/tests/{test.Id}/questions/{theory.Id}/responses"));
            Assert.Equal(2, responses.Responses.Count);
            Assert.NotNull(responses.MarkingGuide);
            var good = responses.Responses.Single(r => r.Answered);
            var blank = responses.Responses.Single(r => !r.Answered);
            await (await teacher.PutAsJsonAsync($"/api/cbt/attempts/{good.AttemptId}/items/{good.ItemId}/mark", new { score = 6 }))
                .AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
            var review = await AdmissionsKit.ReadAsync<AttemptReview>(await teacher.PutAsJsonAsync($"/api/cbt/attempts/{good.AttemptId}/items/{good.ItemId}/mark",
                new { score = 4, comment = "Good, but name precipitation." }));
            Assert.Equal((7m, 0), (review.Score!.Value, review.NeedsMarking));
            Assert.Contains(review.Items, i => i.Kind == QuestionKind.Objective && i.Options.Any(o => o.IsCorrect && o.Chosen));
            (await teacher.PutAsJsonAsync($"/api/cbt/attempts/{blank.AttemptId}/items/{blank.ItemId}/mark", new { score = 0 })).EnsureSuccessStatusCode();

            // Outsiders can't see or mark.
            var (outsider, _) = await SchoolBuilder.TeacherAsync(Api);
            await (await outsider.GetAsync($"/api/cbt/attempts/{good.AttemptId}")).AssertProblemAsync(HttpStatusCode.NotFound);

            // Before release the student sees no score, and nothing can go to the grade sheet.
            Assert.Null((await AdmissionsKit.ReadAsync<AttemptPaper>(await first.GetAsync($"/api/student/attempts/{paper.Id}"))).Score);
            await (await teacher.GetAsync($"/api/cbt/tests/{test.Id}/gradebook")).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "test.not_released");

            var released = await AdmissionsKit.ReadAsync<TestDetail>(await teacher.PostAsync($"/api/cbt/tests/{test.Id}/release", null));
            Assert.NotNull(released.ResultsReleasedAt);
            await (await teacher.PutAsJsonAsync($"/api/cbt/attempts/{good.AttemptId}/items/{good.ItemId}/mark", new { score = 5 }))
                .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "test.released");

            var mine = await AdmissionsKit.ReadAsync<AttemptPaper>(await first.GetAsync($"/api/student/attempts/{paper.Id}"));
            Assert.Equal((true, 7m, 8m), (mine.Released, mine.Score!.Value, mine.TotalMarks));
            Assert.Contains(mine.Items, i => i.Comment == "Good, but name precipitation.");
            Assert.Equal(7m, (await AdmissionsKit.ReadAsync<List<StudentTestDto>>(await first.GetAsync("/api/student/tests"))).Single(x => x.Id == test.Id).Score);
            var portal = await AdmissionsKit.ReadAsync<List<ChildTestResult>>(await first.GetAsync($"/api/portal/children/{s.Students[0]}/tests"));
            Assert.Equal(87.5m, Assert.Single(portal, r => r.TestId == test.Id).Percent);

            // Gradebook: preview, then push; the scaled score lands in the component.
            var preview = await AdmissionsKit.ReadAsync<GradebookPreview>(await teacher.GetAsync($"/api/cbt/tests/{test.Id}/gradebook"));
            var row = preview.Rows.Single(r => r.StudentId == s.Students[0]);
            Assert.Equal((7m, Math.Round(7m / 8 * component.MaxScore, 2, MidpointRounding.AwayFromZero), PushState.Ready), (row.Raw, row.Scaled, row.State));
            var pushed = await AdmissionsKit.ReadAsync<PushResult>(await teacher.PostAsJsonAsync($"/api/cbt/tests/{test.Id}/gradebook", new { studentIds = (Guid[]?)null }));
            Assert.Equal((2, 0), (pushed.Pushed, pushed.Skipped.Count));
            await Api.WithServicesAsync(async sp =>
            {
                var db = sp.GetRequiredService<AppDbContext>();
                var score = await db.SubjectScores.Include(x => x.Components)
                    .SingleAsync(x => x.StudentId == s.Students[0] && x.TermId == s.TermId && x.SubjectId == s.SubjectId);
                Assert.Equal(row.Scaled, score.Components.Single(c => c.ComponentId == component.Id).Score);
            });

            var analytics = await AdmissionsKit.ReadAsync<TestAnalytics>(await teacher.GetAsync($"/api/cbt/tests/{test.Id}/analytics"));
            Assert.Equal((2, 7m, 0m, 3), (analytics.Students, analytics.Highest!.Value, analytics.Lowest!.Value, analytics.Items.Count));
            Assert.Equal(2, analytics.Distribution.Sum(b => b.Students));
            Assert.Equal(1, analytics.Items.Single(i => i.Kind == QuestionKind.Objective).Options.Single(o => o.IsCorrect).Picks);
        }
        finally
        {
            Api.Clock.Now = original;
        }
    }

    /// <summary>A published 3-question test (objective 2 marks, gap 1 mark, theory 5 marks) for the school's class, open now on the fake clock.</summary>
    private async Task<(School School, TestDetail Test, Dictionary<string, QuestionDto> Questions)> PublishedTestAsync(Guid? componentId = null)
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
            title = $"CAT {tag}", subjectId = s.SubjectId, classLevelId = level, termId = s.TermId, componentId, classRoomIds = new[] { s.ClassRoomId }, durationMinutes = 45,
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
        Assert.Equal(["Objective", "Gap", "Theory"], published.Sections.Select(x => x.Title)); // publishing keeps the teacher's order
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
