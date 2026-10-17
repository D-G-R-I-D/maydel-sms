using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Academics;
using Maydel.Application.Admissions;
using Maydel.Application.Common;
using Maydel.Application.Finance;
using Maydel.Domain.Admissions;
using Maydel.Domain.Common;

namespace Maydel.Api.Tests.Admissions;

/// <summary>E11.4: open admissions, register an applicant account, pay for the form online or with a school code.</summary>
[Collection(nameof(ApiCollection))]
public sealed class AdmissionFormTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task A_family_registers_signs_in_and_unlocks_the_form_with_a_school_code()
    {
        var (session, level) = await OpenSessionAsync(fee: 10000);
        var (email, application) = await RegisterAsync(session, level);
        Assert.Equal((ApplicationStatus.AwaitingPayment, 10000m), (application.Status, application.FormFee));
        Assert.StartsWith("APP-", application.ReferenceNumber);

        var family = await SignInAsync(email);
        Assert.Equal(application.Id, Assert.Single((await family.GetFromJsonAsync<List<ApplicationDto>>("/api/admissions/my-applications", Json.Options))!).Id);

        var bursar = await Api.ClientAsync(Roles.Bursar);
        var code = Assert.Single((await (await bursar.PostAsJsonAsync("/api/admissions/form-codes", new { sessionId = session, count = 1, amountPaid = 10000, issuedTo = "Mrs. Walk-in" }))
            .Content.ReadFromJsonAsync<List<FormCodeDto>>(Json.Options))!);

        await (await family.PostAsJsonAsync($"/api/admissions/applications/{application.Id}/redeem-code", new { code = "AAAA-BBBB" }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        var paid = await ReadAsync<ApplicationDto>(await family.PostAsJsonAsync($"/api/admissions/applications/{application.Id}/redeem-code", new { code = code.Code.ToLowerInvariant().Replace("-", " ") }));

        Assert.Equal((ApplicationStatus.FormPaid, FormPaymentMethod.SchoolCode), (paid.Status, paid.FormPaymentMethod));
        Assert.Equal(FakeSchoolClock.Default.AddDays(5), paid.ExamWindowEndsOn);
        await (await family.PostAsJsonAsync($"/api/admissions/applications/{application.Id}/redeem-code", new { code = code.Code }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "admission.already_paid");
        var codes = (await bursar.GetFromJsonAsync<PagedResult<FormCodeDto>>($"/api/admissions/form-codes?sessionId={session}&used=true", Json.Options))!;
        Assert.Equal(application.ReferenceNumber, Assert.Single(codes.Items, c => c.Id == code.Id).UsedByReference);
    }

    [Fact]
    public async Task Paying_online_unlocks_the_form_once_through_the_shared_webhook()
    {
        var (session, level) = await OpenSessionAsync(fee: 7500);
        var (email, application) = await RegisterAsync(session, level);
        var family = await SignInAsync(email);

        var checkout = await ReadAsync<OnlineCheckout>(await family.PostAsync($"/api/admissions/applications/{application.Id}/pay", null));
        Assert.StartsWith("MSF-", checkout.Reference);
        Assert.Equal(750000, Api.Paystack.Started[checkout.Reference]);

        Api.Paystack.Pay(checkout.Reference);
        var outcome = await ReadAsync<FormPayments.Outcome>(await family.GetAsync($"/api/admissions/payments/{checkout.Reference}"));
        Assert.Equal(FormPaymentStatus.Succeeded, outcome.Status);
        Assert.Equal(ApplicationStatus.FormPaid, Assert.Single((await family.GetFromJsonAsync<List<ApplicationDto>>("/api/admissions/my-applications", Json.Options))!).Status);
    }

    [Fact]
    public async Task Closed_sessions_existing_emails_and_bots_are_turned_away()
    {
        var (session, level) = await OpenSessionAsync(fee: 5000);
        var (email, _) = await RegisterAsync(session, level);

        await (await Register(session, level, email)).AssertProblemAsync(HttpStatusCode.Conflict, "admission.email_in_use");
        await (await Register(session, level, $"bot-{Guid.NewGuid():N}@example.com", website: "http://spam")).AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        var admin = await Api.ClientAsync(Roles.Admin);
        (await admin.PutAsJsonAsync("/api/admissions/settings", new { sessionId = session, isOpen = false, formFee = 5000, examWindowDays = 5 })).EnsureSuccessStatusCode();
        await (await Register(session, level, $"late-{Guid.NewGuid():N}@example.com")).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "admission.closed");
    }

    [Fact]
    public async Task Families_only_see_their_own_applications_and_staff_see_all()
    {
        var (session, level) = await OpenSessionAsync(fee: 5000);
        var (_, mine) = await RegisterAsync(session, level);
        var (otherEmail, _) = await RegisterAsync(session, level);
        var other = await SignInAsync(otherEmail);

        await (await other.PostAsync($"/api/admissions/applications/{mine.Id}/pay", null)).AssertProblemAsync(HttpStatusCode.NotFound, "admission.not_found");
        Assert.DoesNotContain((await other.GetFromJsonAsync<List<ApplicationDto>>("/api/admissions/my-applications", Json.Options))!, a => a.Id == mine.Id);
        var list = (await (await Api.ClientAsync(Roles.Bursar)).GetFromJsonAsync<PagedResult<ApplicationDto>>($"/api/admissions/applications?sessionId={session}&pageSize=200", Json.Options))!;
        Assert.Contains(list.Items, a => a.Id == mine.Id);
        await (await (await Api.ClientAsync(Roles.Teacher)).GetAsync($"/api/admissions/applications?sessionId={session}")).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await (await Api.ClientAsync(Roles.Bursar)).PutAsJsonAsync("/api/admissions/settings", new { sessionId = session, isOpen = true, formFee = 1, examWindowDays = 5 }))
            .AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_existing_parent_applies_for_a_sibling_from_their_account()
    {
        var (session, level) = await OpenSessionAsync(fee: 5000);
        var parent = await Api.ClientAsync(Roles.Parent);
        var response = await parent.PostAsJsonAsync("/api/admissions/applications", new { childFirstName = "Ebuka", childLastName = "Sibling", classLevelId = level, sessionId = session });
        Assert.Equal(ApplicationStatus.AwaitingPayment, (await ReadAsync<ApplicationDto>(response)).Status);
    }

    // ---------- helpers ----------

    private static int _year = 8000 + Random.Shared.Next(0, 900);

    private async Task<(Guid Session, Guid Level)> OpenSessionAsync(decimal fee)
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var year = Interlocked.Increment(ref _year);
        var session = (await (await admin.PostAsJsonAsync("/api/academics/sessions", new { name = $"{year}/{year + 1}", startsOn = $"{year}-09-14", endsOn = $"{year + 1}-07-23" }))
            .Content.ReadFromJsonAsync<SessionDto>(Json.Options))!;
        (await admin.PutAsJsonAsync("/api/admissions/settings", new { sessionId = session.Id, isOpen = true, formFee = fee, examWindowDays = 5, instructions = "Bring a passport photo." })).EnsureSuccessStatusCode();
        var info = (await Api.CreateClient().GetFromJsonAsync<List<AdmissionsInfo>>("/api/public/admissions", Json.Options))!;
        var open = Assert.Single(info, i => i.SessionId == session.Id);
        return (session.Id, open.ClassLevels.First().Id);
    }

    private Task<HttpResponseMessage> Register(Guid session, Guid level, string email, string? website = null) =>
        Api.CreateClient().PostAsJsonAsync("/api/public/admissions/register", new
        {
            parentName = "Mrs. Applicant", email, phone = "08031234567", password = "Apply#Pass2026", childFirstName = "Ada", childLastName = "Newchild",
            childDateOfBirth = "2020-05-05", classLevelId = level, sessionId = session, website,
        });

    private async Task<(string Email, ApplicationDto Application)> RegisterAsync(Guid session, Guid level)
    {
        var email = $"applicant-{Guid.NewGuid():N}@example.com";
        return (email, await ReadAsync<ApplicationDto>(await Register(session, level, email)));
    }

    private async Task<HttpClient> SignInAsync(string email) => Api.CreateClient((await Api.LoginAsync(email, "Apply#Pass2026")).AccessToken);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(Json.Options))!;
    }
}
