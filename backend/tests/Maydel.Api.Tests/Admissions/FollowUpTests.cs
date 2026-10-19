using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Academics;
using Maydel.Application.Admissions;
using Maydel.Application.Admissions.EnrolApplicant;
using Maydel.Application.Admissions.RegisterWithGoogle;
using Maydel.Domain.Admissions;
using Maydel.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using static Maydel.Api.Tests.Admissions.AdmissionsKit;

namespace Maydel.Api.Tests.Admissions;

/// <summary>Google sign-up for admissions, entry terms for mid-session intakes, and enrolment telling the admin about parent logins.</summary>
[Collection(nameof(ApiCollection))]
public sealed class FollowUpTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task Families_can_buy_the_form_with_Google_and_sign_in_with_Google_afterwards()
    {
        var session = await OpenSessionAsync(Api);
        var level = (await (await Api.ClientAsync(Roles.Admin)).GetFromJsonAsync<List<ClassLevelDto>>("/api/academics/class-levels", Json.Options))!.First().Id;
        var email = $"google-{Guid.NewGuid():N}@gmail.com";
        var subject = Guid.NewGuid().ToString("N");

        var signedUp = await ReadAsync<GoogleRegistration>(await Api.CreateClient().PostAsJsonAsync("/api/public/admissions/register/google", new
        {
            idToken = Api.Google.Issue(email, subject), parentName = "Mrs. Okafor", phone = "08035556666", childFirstName = "Ngozi", childLastName = "Okafor",
            classLevelId = level, sessionId = session,
        }));
        Assert.Equal((email, ApplicationStatus.AwaitingPayment), (signedUp.Application.Email, signedUp.Application.Status));
        Assert.False(string.IsNullOrEmpty(signedUp.Session.AccessToken));
        var family = Api.CreateClient(signedUp.Session.AccessToken);
        Assert.Contains(await ReadAsync<List<ApplicationDto>>(await family.GetAsync("/api/admissions/my-applications")), a => a.Id == signedUp.Application.Id);

        // Next time: plain Google sign-in finds the same account.
        var again = await Api.CreateClient().PostAsJsonAsync("/api/auth/google", new { idToken = Api.Google.Issue(email, subject) });
        again.EnsureSuccessStatusCode();

        // The same email can't sign up twice; an unverified Google email is refused.
        await (await Api.CreateClient().PostAsJsonAsync("/api/public/admissions/register/google", new
        {
            idToken = Api.Google.Issue(email, subject), parentName = "x", phone = "080", childFirstName = "a", childLastName = "b", classLevelId = level, sessionId = session,
        })).AssertProblemAsync(HttpStatusCode.Conflict, "admission.email_in_use");
        await (await Api.CreateClient().PostAsJsonAsync("/api/public/admissions/register/google", new
        {
            idToken = Api.Google.Issue($"x{email}", emailVerified: false), parentName = "x", phone = "080", childFirstName = "a", childLastName = "b",
            classLevelId = level, sessionId = session,
        })).AssertProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Entry_terms_set_the_resumption_date_and_admission_date_and_enrolment_reports_parent_login_problems()
    {
        // 2027/2028 with three terms: admits join in the second term.
        var admin = await Api.ClientAsync(Roles.Admin);
        var session = await OpenSessionAsync(Api, 2028);
        (await admin.PostAsJsonAsync($"/api/academics/sessions/{session}/terms", new { number = "First", startsOn = "2028-09-13", endsOn = "2028-12-10" })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/academics/sessions/{session}/terms", new { number = "Second", startsOn = "2029-01-08", endsOn = "2029-04-05" })).EnsureSuccessStatusCode();
        var second = (await admin.GetFromJsonAsync<List<SessionDto>>("/api/academics/sessions", Json.Options))!.Single(s => s.Id == session).Terms.Single(t => t.Number == Domain.Academics.TermNumber.Second);
        var settings = await ReadAsync<AdmissionSettingsDto>(await admin.PutAsJsonAsync("/api/admissions/settings",
            new { sessionId = session, isOpen = true, formFee = 5000, examWindowDays = 5, entryTermId = second.Id }));
        Assert.Equal((second.Id, new DateOnly(2029, 1, 8)), (settings.EntryTermId!.Value, settings.EntryStartsOn!.Value));
        await (await admin.PutAsJsonAsync("/api/admissions/settings", new { sessionId = session, isOpen = true, formFee = 5000, examWindowDays = 5, entryTermId = Guid.NewGuid() }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        Assert.Equal(new DateOnly(2028, 9, 13),
            (await ReadAsync<AdmissionSettingsDto>(await admin.PutAsJsonAsync("/api/admissions/settings", new { sessionId = session, isOpen = true, formFee = 5000, examWindowDays = 5 }))).EntryStartsOn);
    }

    [Fact]
    public async Task Enrolment_says_when_the_phone_already_belongs_to_another_parent_login()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        // An existing parent with a login owns this phone.
        var s = await SchoolBuilder.SetUpAsync(Api);
        var phone = $"0803{Random.Shared.Next(1_000_000, 9_999_999)}";
        var added = await (await admin.PostAsJsonAsync($"/api/students/{s.Students[0]}/guardians",
            new { fullName = "Mr. Existing", phone, email = $"existing-{Guid.NewGuid():N}@example.com", relationship = "Father", isPrimaryContact = true, canPickUp = true }))
            .Content.ReadFromJsonAsync<Maydel.Application.Students.GuardianAdded>(Json.Options);
        (await admin.PostAsync($"/api/guardians/{added!.GuardianId}/login", null)).EnsureSuccessStatusCode();

        // A different family account applies with that same phone; once accepted and enrolled the admin is told.
        var session = await OpenSessionAsync(Api, 2027); // admission must be within a year of the fake today
        var slot = await SlotAsync(admin, session, Wat(19, 9), capacity: 5, venue: "Hall");
        var (family, id) = await SubmittedAsync(Api, session);
        await Api.WithServicesAsync(async sp =>
        {
            var db = sp.GetRequiredService<Maydel.Infrastructure.Persistence.AppDbContext>();
            var app = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.Applications, a => a.Id == id);
            app.Phone = phone;
            await db.SaveChangesAsync();
        });
        (await family.PostAsJsonAsync($"/api/admissions/applications/{id}/book-exam", new { slotId = slot.Id })).EnsureSuccessStatusCode();
        (await (await Api.ClientAsync(Roles.Teacher)).PutAsJsonAsync($"/api/admissions/applications/{id}/exam-score", new { score = 90 })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync("/api/admissions/decisions", new { applicationIds = new[] { id }, decision = "Offer" })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync("/api/admissions/decisions/publish", new { sessionId = session })).EnsureSuccessStatusCode();
        (await family.PostAsync($"/api/admissions/applications/{id}/accept", null)).EnsureSuccessStatusCode();
        var enrolled = await ReadAsync<EnrolmentResult>(await admin.PostAsJsonAsync($"/api/admissions/applications/{id}/enrol", new { classRoomId = (Guid?)null }));
        Assert.Equal(ParentLoginOutcome.PhoneBelongsToAnotherAccount, enrolled.ParentLogin);
        Assert.Contains("already belongs to another parent account", enrolled.Message);
    }
}
