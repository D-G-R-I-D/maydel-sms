using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Academics;
using Maydel.Application.Admissions;
using Maydel.Application.Admissions.NotifyStageOnePasses;
using Maydel.Application.Admissions.SweepAdmissions;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Admissions;
using Maydel.Domain.Common;
using Maydel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Maydel.Api.Tests.Admissions;

/// <summary>E11.6–7: exam sittings, booking with capacity, scores/absence, reopen, the sweep, cut-off lists and pass emails.</summary>
[Collection(nameof(ApiCollection))]
public sealed class EntranceExamTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;


    [Fact]
    public async Task Families_book_sittings_within_their_window_and_capacity()
    {
        var session = await OpenSessionAsync();
        var admin = await Api.ClientAsync(Roles.Admin);
        var small = await SlotAsync(admin, session, Wat(19, 9), capacity: 1, venue: "Hall A");
        var big = await SlotAsync(admin, session, Wat(20, 10), capacity: 5, venue: "ICT Lab", mode: "Cbt");
        await SlotAsync(admin, session, Wat(25, 9), capacity: 5, venue: "Hall B"); // after everyone's window
        await (await admin.PostAsJsonAsync("/api/admissions/exam-slots", SlotBody(session, Wat(14, 9), 5, "Old")))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation");

        var (first, firstId) = await SubmittedAsync(session);
        var (second, secondId) = await SubmittedAsync(session);
        var (unsubmitted, unsubmittedId) = await PaidAsync(session);

        var options = await ReadAsync<List<ExamSlotOption>>(await first.GetAsync($"/api/admissions/applications/{firstId}/exam-slots"));
        Assert.Equal([small.Id, big.Id], options.Select(o => o.Id));
        await (await unsubmitted.PostAsJsonAsync($"/api/admissions/applications/{unsubmittedId}/book-exam", new { slotId = big.Id }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "admission.submit_first");

        var booked = await ReadAsync<ApplicationDetail>(await first.PostAsJsonAsync($"/api/admissions/applications/{firstId}/book-exam", new { slotId = small.Id }));
        Assert.Equal((ApplicationStatus.ExamBooked, "Hall A"), (booked.Application.Status, booked.Application.Exam?.Venue));

        // The single seat is gone for the next family.
        Assert.Equal([big.Id], (await ReadAsync<List<ExamSlotOption>>(await second.GetAsync($"/api/admissions/applications/{secondId}/exam-slots"))).Select(o => o.Id));
        await (await second.PostAsJsonAsync($"/api/admissions/applications/{secondId}/book-exam", new { slotId = small.Id }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        await ReadAsync<ApplicationDetail>(await second.PostAsJsonAsync($"/api/admissions/applications/{secondId}/book-exam", new { slotId = big.Id }));
        await (await second.GetAsync($"/api/admissions/applications/{firstId}/exam-slots")).AssertProblemAsync(HttpStatusCode.NotFound);

        // Booked sittings can't be moved or deleted; seats can't drop below bookings.
        await (await admin.DeleteAsync($"/api/admissions/exam-slots/{small.Id}")).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "exam_slot.has_bookings");
        await (await admin.PutAsJsonAsync($"/api/admissions/exam-slots/{small.Id}", SlotBody(session, Wat(19, 11), 1, "Hall A")))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "exam_slot.has_bookings");
        var slots = await ReadAsync<List<ExamSlotDto>>(await admin.GetAsync($"/api/admissions/exam-slots?sessionId={session}"));
        Assert.Equal([1, 1, 0], slots.Select(s => s.Booked));

        // Reminders go out for sittings in the next 24 hours, once.
        var reminders = await SweepAsync(new DateTimeOffset(2026, 10, 18, 10, 0, 0, TimeSpan.Zero), session);
        Assert.Equal(new SweepResult(0, 1), reminders);
        Assert.Equal(new SweepResult(0, 0), await SweepAsync(new DateTimeOffset(2026, 10, 18, 11, 0, 0, TimeSpan.Zero), session));
        Assert.Equal(2, await OutboxCountAsync(firstId, "%exam%")); // booked + reminder

        // After the window, the unbooked application is marked Missed (once), and the school can reopen it.
        Assert.Equal(new SweepResult(1, 0), await SweepAsync(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero), session));
        var reopened = await ReadAsync<ApplicationDetail>(await admin.PostAsync($"/api/admissions/applications/{unsubmittedId}/reopen", null));
        Assert.Equal((ApplicationStatus.FormPaid, new DateOnly(2026, 10, 20)), (reopened.Application.Status, reopened.Application.ExamWindowEndsOn));
        await (await admin.PostAsync($"/api/admissions/applications/{firstId}/reopen", null)).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "admission.not_missed");
    }

    [Fact]
    public async Task Scores_and_the_cut_off_drive_the_lists_and_the_first_stage_emails()
    {
        var session = await OpenSessionAsync();
        var admin = await Api.ClientAsync(Roles.Admin);
        var slot = await SlotAsync(admin, session, Wat(19, 9), capacity: 10, venue: "Hall A");
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var (family, id) = await SubmittedAsync(session);
            (await family.PostAsJsonAsync($"/api/admissions/applications/{id}/book-exam", new { slotId = slot.Id })).EnsureSuccessStatusCode();
            ids.Add(id);
        }
        Assert.Equal(3, (await ReadAsync<List<ExamCandidate>>(await admin.GetAsync($"/api/admissions/exam-slots/{slot.Id}/candidates"))).Count);

        var teacher = await Api.ClientAsync(Roles.Teacher);
        Assert.Equal(82.5m, (await ReadAsync<ExamCandidate>(await teacher.PutAsJsonAsync($"/api/admissions/applications/{ids[0]}/exam-score", new { score = 82.5 }))).Score);
        await ReadAsync<ExamCandidate>(await teacher.PutAsJsonAsync($"/api/admissions/applications/{ids[1]}/exam-score", new { score = 61 }));
        Assert.Equal(ApplicationStatus.Missed, (await ReadAsync<ExamCandidate>(await teacher.PostAsync($"/api/admissions/applications/{ids[2]}/absent", null))).Status);
        await (await teacher.PutAsJsonAsync($"/api/admissions/applications/{ids[1]}/exam-score", new { score = 101 })).AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        await (await (await Api.ClientAsync(Roles.Bursar)).PutAsJsonAsync($"/api/admissions/applications/{ids[1]}/exam-score", new { score = 70 }))
            .AssertProblemAsync(HttpStatusCode.Forbidden);

        // No cut-off yet: everyone who sat is listed; nobody can be told they passed.
        var results = await ReadAsync<ExamResults>(await admin.GetAsync($"/api/admissions/results?sessionId={session}"));
        Assert.Equal((2, 0, 0, 1), (results.Taken.Count, results.Passed.Count, results.Failed.Count, results.Missed));
        await (await admin.PostAsJsonAsync("/api/admissions/results/notify-passed", new { sessionId = session })).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "admission.no_cut_off");

        // Changing the cut-off recomputes the lists before anything is sent.
        var strict = await ReadAsync<ExamResults>(await admin.PutAsJsonAsync("/api/admissions/results/cut-off", new { sessionId = session, cutOffMark = 85 }));
        Assert.Equal((0, 2), (strict.Passed.Count, strict.Failed.Count));
        var fair = await ReadAsync<ExamResults>(await admin.PutAsJsonAsync("/api/admissions/results/cut-off", new { sessionId = session, cutOffMark = 60 }));
        Assert.Equal((2, 0), (fair.Passed.Count, fair.Failed.Count));
        var settled = await ReadAsync<ExamResults>(await admin.PutAsJsonAsync("/api/admissions/results/cut-off", new { sessionId = session, cutOffMark = 70 }));
        Assert.Equal((ids[0], ids[1]), (Assert.Single(settled.Passed).ApplicationId, Assert.Single(settled.Failed).ApplicationId));

        Assert.Equal(1, (await ReadAsync<NotifyResult>(await admin.PostAsJsonAsync("/api/admissions/results/notify-passed", new { sessionId = session }))).Queued);
        Assert.Equal(0, (await ReadAsync<NotifyResult>(await admin.PostAsJsonAsync("/api/admissions/results/notify-passed", new { sessionId = session }))).Queued);
        Assert.Equal(1, await OutboxCountAsync(ids[0], "%first stage passed%"));
        Assert.Equal(0, await OutboxCountAsync(ids[1], "%first stage passed%"));

        var csv = await admin.GetAsync($"/api/admissions/results.csv?sessionId={session}");
        csv.EnsureSuccessStatusCode();
        var text = await csv.Content.ReadAsStringAsync();
        Assert.Contains("82.5,Passed", text);
        Assert.Contains("61,Failed", text);
        await (await teacher.GetAsync($"/api/admissions/results?sessionId={session}")).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    // ---------- helpers (see AdmissionsKit) ----------

    private static string Wat(int day, int hour) => AdmissionsKit.Wat(day, hour);
    private Task<Guid> OpenSessionAsync() => AdmissionsKit.OpenSessionAsync(Api);
    private static object SlotBody(Guid session, string startsAt, int capacity, string venue, string mode = "Paper") => AdmissionsKit.SlotBody(session, startsAt, capacity, venue, mode);
    private static Task<ExamSlotDto> SlotAsync(HttpClient admin, Guid session, string startsAt, int capacity, string venue, string mode = "Paper") =>
        AdmissionsKit.SlotAsync(admin, session, startsAt, capacity, venue, mode);
    private Task<(HttpClient Family, Guid Application)> PaidAsync(Guid session) => AdmissionsKit.PaidAsync(Api, session);
    private Task<(HttpClient Family, Guid Application)> SubmittedAsync(Guid session) => AdmissionsKit.SubmittedAsync(Api, session);
    private Task<SweepResult> SweepAsync(DateTimeOffset now, Guid session) => AdmissionsKit.SweepAsync(Api, now, session);
    private Task<int> OutboxCountAsync(Guid application, string subjectLike) => AdmissionsKit.OutboxCountAsync(Api, application, subjectLike);
    private static Task<T> ReadAsync<T>(HttpResponseMessage response) => AdmissionsKit.ReadAsync<T>(response);
}
