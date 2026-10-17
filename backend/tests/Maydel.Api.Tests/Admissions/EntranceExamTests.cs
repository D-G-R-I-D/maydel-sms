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

    // The fake clock: 08:00 in Abuja on Thursday 15 October 2026; a 5-day window ends on the 20th.
    private static string Wat(int day, int hour) => $"2026-10-{day:00}T{hour:00}:00:00+01:00";

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

    // ---------- helpers ----------

    private static int _year = 2850 + Random.Shared.Next(0, 100);

    private async Task<Guid> OpenSessionAsync()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var year = Interlocked.Increment(ref _year);
        var session = (await (await admin.PostAsJsonAsync("/api/academics/sessions", new { name = $"{year}/{year + 1}", startsOn = $"{year}-09-14", endsOn = $"{year + 1}-07-23" }))
            .Content.ReadFromJsonAsync<SessionDto>(Json.Options))!;
        (await admin.PutAsJsonAsync("/api/admissions/settings", new { sessionId = session.Id, isOpen = true, formFee = 5000, examWindowDays = 5 })).EnsureSuccessStatusCode();
        return session.Id;
    }

    private static object SlotBody(Guid session, string startsAt, int capacity, string venue, string mode = "Paper") =>
        new { sessionId = session, startsAt, durationMinutes = 90, venue, mode, capacity };

    private static async Task<ExamSlotDto> SlotAsync(HttpClient admin, Guid session, string startsAt, int capacity, string venue, string mode = "Paper") =>
        await ReadAsync<ExamSlotDto>(await admin.PostAsJsonAsync("/api/admissions/exam-slots", SlotBody(session, startsAt, capacity, venue, mode)));

    private async Task<(HttpClient Family, Guid Application)> PaidAsync(Guid session)
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var level = (await admin.GetFromJsonAsync<List<ClassLevelDto>>("/api/academics/class-levels", Json.Options))!.First().Id;
        var email = $"exam-{Guid.NewGuid():N}@example.com";
        var application = await ReadAsync<ApplicationDto>(await Api.CreateClient().PostAsJsonAsync("/api/public/admissions/register", new
        {
            parentName = "Mrs. Bello", email, phone = "08031234567", password = "Apply#Pass2026", childFirstName = "Amina", childLastName = "Bello",
            classLevelId = level, sessionId = session,
        }));
        var family = Api.CreateClient((await Api.LoginAsync(email, "Apply#Pass2026")).AccessToken);
        var code = (await (await (await Api.ClientAsync(Roles.Bursar)).PostAsJsonAsync("/api/admissions/form-codes", new { sessionId = session, count = 1, amountPaid = 5000 }))
            .Content.ReadFromJsonAsync<List<FormCodeDto>>(Json.Options))!.Single().Code;
        (await family.PostAsJsonAsync($"/api/admissions/applications/{application.Id}/redeem-code", new { code })).EnsureSuccessStatusCode();
        return (family, application.Id);
    }

    private async Task<(HttpClient Family, Guid Application)> SubmittedAsync(Guid session)
    {
        var (family, id) = await PaidAsync(session);
        (await family.PutAsJsonAsync($"/api/admissions/applications/{id}/form", new
        {
            childFirstName = "Amina", childLastName = "Bello", childDateOfBirth = "2019-06-06",
            form = new { childSex = "Female", homeAddress = "3 Close, Wuse, Abuja", motherName = "Mrs. Bello", motherPhone = "08030002222", declaration = true },
        })).EnsureSuccessStatusCode();
        var upload = new MultipartFormDataContent { { new StringContent("PassportPhoto"), "kind" } };
        var photo = new ByteArrayContent(Jpeg());
        photo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        upload.Add(photo, "file", "photo");
        (await family.PostAsync($"/api/admissions/applications/{id}/documents", upload)).EnsureSuccessStatusCode();
        (await family.PostAsync($"/api/admissions/applications/{id}/submit", null)).EnsureSuccessStatusCode();
        return (family, id);
    }

    private async Task<SweepResult> SweepAsync(DateTimeOffset now, Guid session)
    {
        SweepResult? result = null;
        await Api.WithServicesAsync(async sp =>
            result = (await sp.GetRequiredService<ICommandHandler<SweepAdmissionsCommand, SweepResult>>().Handle(new SweepAdmissionsCommand(now, session), CancellationToken.None)).Value);
        return result!;
    }

    private async Task<int> OutboxCountAsync(Guid application, string subjectLike)
    {
        var count = 0;
        await Api.WithServicesAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var reference = await db.Applications.Where(a => a.Id == application).Select(a => a.ReferenceNumber).SingleAsync();
            count = await db.OutboxEmails.CountAsync(e => e.Subject.Contains(reference) && EF.Functions.ILike(e.Subject, subjectLike));
        });
        return count;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode) Assert.Fail($"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json.Options))!;
    }

    private static byte[] Jpeg()
    {
        using var surface = SKSurface.Create(new SKImageInfo(300, 400));
        surface.Canvas.Clear(SKColors.SkyBlue);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
        return data.ToArray();
    }
}
