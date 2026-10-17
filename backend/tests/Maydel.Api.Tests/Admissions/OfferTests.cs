using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Academics;
using Maydel.Application.Admissions;
using Maydel.Application.Admissions.EnrolApplicant;
using Maydel.Application.Admissions.PublishDecisions;
using Maydel.Application.Admissions.SweepAdmissions;
using Maydel.Application.Identity;
using Maydel.Application.Notifications;
using Maydel.Application.Portal;
using Maydel.Domain.Admissions;
using Maydel.Domain.Common;
using Maydel.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UglyToad.PdfPig;
using static Maydel.Api.Tests.Admissions.AdmissionsKit;

namespace Maydel.Api.Tests.Admissions;

/// <summary>E11.8: draft decisions, publish (letters, regret emails), accept/decline, enrol into a student + parent login, NDPR purge.</summary>
[Collection(nameof(ApiCollection))]
public sealed class OfferTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task Offers_are_published_letters_sent_and_accepted_offers_enrol_the_child()
    {
        // 2027/2028: close enough to the fake today that the child can be admitted from the session start.
        var session = await OpenSessionAsync(Api, 2027);
        var admin = await Api.ClientAsync(Roles.Admin);
        var slot = await SlotAsync(admin, session, Wat(19, 9), capacity: 10, venue: "Hall A");
        var teacher = await Api.ClientAsync(Roles.Teacher);
        var families = new List<(HttpClient Client, Guid Id)>();
        foreach (var score in new[] { 81, 74, 38 })
        {
            var (family, id) = await SubmittedAsync(Api, session);
            (await family.PostAsJsonAsync($"/api/admissions/applications/{id}/book-exam", new { slotId = slot.Id })).EnsureSuccessStatusCode();
            (await teacher.PutAsJsonAsync($"/api/admissions/applications/{id}/exam-score", new { score })).EnsureSuccessStatusCode();
            families.Add((family, id));
        }
        var (top, second, low) = (families[0], families[1], families[2]);

        // Draft: ticked, but the family sees nothing yet.
        var ticked = await ReadAsync<List<ExamCandidate>>(await admin.PutAsJsonAsync("/api/admissions/decisions",
            new { applicationIds = new[] { top.Id, second.Id }, decision = "Offer" }));
        Assert.All(ticked, c => Assert.Equal(OfferDecision.Offer, c.Decision));
        Assert.Equal(ApplicationStatus.Assessed, (await ReadAsync<ApplicationDetail>(await top.Client.GetAsync($"/api/admissions/applications/{top.Id}"))).Application.Status);
        await (await top.Client.GetAsync($"/api/admissions/applications/{top.Id}/letter.pdf")).AssertProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal(0, await OutboxCountAsync(Api, top.Id, "%offer%"));

        var published = await ReadAsync<PublishDecisionsResult>(await admin.PostAsJsonAsync("/api/admissions/decisions/publish", new { sessionId = session }));
        Assert.Equal(new PublishDecisionsResult(2, 1), published);
        await (await admin.PostAsJsonAsync("/api/admissions/decisions/publish", new { sessionId = session }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "admission.nothing_to_publish");

        var offer = (await ReadAsync<ApplicationDetail>(await top.Client.GetAsync($"/api/admissions/applications/{top.Id}"))).Application;
        Assert.Equal(ApplicationStatus.Offered, offer.Status);
        Assert.StartsWith("MSS/2027/", offer.AdmissionNumber);
        Assert.Equal(ApplicationStatus.NotOffered, (await ReadAsync<ApplicationDetail>(await low.Client.GetAsync($"/api/admissions/applications/{low.Id}"))).Application.Status);
        Assert.Equal(1, await OutboxCountAsync(Api, low.Id, "%your application%"));

        // The letter: same content on every reprint, attached to the offer email.
        var first = await LetterTextAsync(top.Client, top.Id);
        Assert.Contains(offer.AdmissionNumber!, first);
        Assert.Contains("OFFER OF ADMISSION", first);
        Assert.Equal(first, await LetterTextAsync(top.Client, top.Id));
        await (await low.Client.GetAsync($"/api/admissions/applications/{top.Id}/letter.pdf")).AssertProblemAsync(HttpStatusCode.NotFound);
        Assert.True(await OfferEmailHasLetterAsync(offer.Email));

        // Second family declines; top family accepts and is enrolled.
        Assert.Equal(ApplicationStatus.Withdrawn,
            (await ReadAsync<ApplicationDetail>(await second.Client.PostAsync($"/api/admissions/applications/{second.Id}/decline", null))).Application.Status);
        await (await admin.PostAsJsonAsync($"/api/admissions/applications/{top.Id}/enrol", new { classRoomId = (Guid?)null }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "admission.not_accepted");
        Assert.Equal(ApplicationStatus.Accepted,
            (await ReadAsync<ApplicationDetail>(await top.Client.PostAsync($"/api/admissions/applications/{top.Id}/accept", null))).Application.Status);

        var otherLevelRoom = (await admin.GetFromJsonAsync<List<ClassRoomDto>>("/api/academics/classrooms", Json.Options))!.First(r => r.ClassLevelId != offer.ClassLevelId);
        await (await admin.PostAsJsonAsync($"/api/admissions/applications/{top.Id}/enrol", new { classRoomId = otherLevelRoom.Id }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        var enrolled = await ReadAsync<EnrolmentResult>(await admin.PostAsJsonAsync($"/api/admissions/applications/{top.Id}/enrol", new { classRoomId = (Guid?)null }));
        Assert.Equal((offer.AdmissionNumber, ApplicationStatus.Enrolled), (enrolled.AdmissionNumber, enrolled.Application.Application.Status));
        await (await admin.PostAsJsonAsync($"/api/admissions/applications/{top.Id}/enrol", new { classRoomId = (Guid?)null }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "admission.not_accepted");

        await Api.WithServicesAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var student = await db.Students.Include(s => s.Guardians).ThenInclude(g => g.Guardian).SingleAsync(s => s.Id == enrolled.StudentId);
            Assert.Equal(("Amina", "Female", "3 Close, Wuse, Abuja"), (student.FirstName, student.Sex.ToString(), student.HomeAddress));
            Assert.Equal(new DateOnly(2027, 9, 13), student.AdmittedOn);
            // Mother (from the form, same phone as no one else) and the applicant contact; the applicant is the primary contact with the login.
            var primary = Assert.Single(student.Guardians, g => g.IsPrimaryContact).Guardian;
            Assert.Equal(offer.Email, primary.Email);
            var user = await sp.GetRequiredService<UserManager<AppUser>>().FindByEmailAsync(offer.Email);
            Assert.Equal(user!.Id, primary.UserId);
        });

        // Same login, now with the parent portal and the child in it.
        var parent = Api.CreateClient((await Api.LoginAsync(offer.Email, "Apply#Pass2026")).AccessToken);
        var children = await ReadAsync<List<PortalChild>>(await parent.GetAsync("/api/portal/children"));
        Assert.Contains(children, c => c.StudentId == enrolled.StudentId);
        Assert.Equal(1, await OutboxCountAsync(Api, top.Id, "%enrolment complete%"));

        // Six months on, the unsuccessful and withdrawn applications lose the child's details and documents.
        var purged = await SweepAsync(Api, new DateTimeOffset(2027, 4, 16, 9, 0, 0, TimeSpan.Zero), session);
        Assert.Equal(2, purged.Purged);
        var gone = await ReadAsync<ApplicationDetail>(await admin.GetAsync($"/api/admissions/applications/{low.Id}/detail"));
        Assert.Equal(("Removed", 0, (string?)null), (gone.Application.ChildFirstName, gone.Documents.Count, gone.Form.HomeAddress));
        Assert.Equal("Amina", (await ReadAsync<ApplicationDetail>(await admin.GetAsync($"/api/admissions/applications/{top.Id}/detail"))).Application.ChildFirstName);
    }

    [Fact]
    public async Task Only_admins_decide_and_only_scored_applicants_can_be_ticked()
    {
        var session = await OpenSessionAsync(Api);
        var (_, id) = await SubmittedAsync(Api, session);
        var admin = await Api.ClientAsync(Roles.Admin);
        await (await admin.PutAsJsonAsync("/api/admissions/decisions", new { applicationIds = new[] { id }, decision = "Offer" }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "admission.not_assessed");
        await (await (await Api.ClientAsync(Roles.Bursar)).PutAsJsonAsync("/api/admissions/decisions", new { applicationIds = new[] { id }, decision = "Offer" }))
            .AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await admin.PostAsJsonAsync("/api/admissions/decisions/publish", new { sessionId = session }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "admission.nothing_to_publish");
    }

    private static async Task<string> LetterTextAsync(HttpClient client, Guid application)
    {
        var response = await client.GetAsync($"/api/admissions/applications/{application}/letter.pdf");
        response.EnsureSuccessStatusCode();
        using var pdf = PdfDocument.Open(await response.Content.ReadAsByteArrayAsync());
        return string.Join("\n", pdf.GetPages().Select(p => p.Text));
    }

    /// <summary>Sends the outbox (fake sender) and checks the offer email went out with the letter attached.</summary>
    private async Task<bool> OfferEmailHasLetterAsync(string email)
    {
        await Api.WithServicesAsync(async sp =>
        {
            var dispatcher = sp.GetRequiredService<EmailDispatcher>();
            while (await dispatcher.DispatchDueAsync(CancellationToken.None) == EmailDispatcher.BatchSize) { }
        });
        return Api.Email.Sent.Any(m => m.To == email && m.Subject.StartsWith("Offer of admission") && m.Attachments.Count == 1
            && m.Attachments[0].ContentType == "application/pdf");
    }
}
