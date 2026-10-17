using System.Net;
using System.Net.Http.Json;
using System.Text;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Academics;
using Maydel.Application.Common;
using Maydel.Application.Finance;
using Maydel.Application.Identity;
using Maydel.Application.Students;
using Maydel.Domain.Common;
using Maydel.Domain.Finance;

namespace Maydel.Api.Tests.Finance;

/// <summary>E10.5 Paystack: start from the portal, confirm by webhook or return page exactly once, re-verify everything.</summary>
[Collection(nameof(ApiCollection))]
public sealed class OnlinePaymentTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task Parent_pays_online_and_the_payment_is_recorded_once_whether_webhook_or_return_page_comes_first()
    {
        var (s, invoice, parent, bursar) = await SetUpAsync(plan: true);

        await (await StartAsync(parent, s.Students[0], invoice, 50000)).AssertProblemAsync(HttpStatusCode.BadRequest, "validation"); // below 60%
        var checkout = await CheckoutAsync(await StartAsync(parent, s.Students[0], invoice, 60000));
        Assert.StartsWith("https://checkout.paystack.test/", checkout.AuthorizationUrl);
        Assert.Equal(6000000, Api.Paystack.Started[checkout.Reference]); // kobo

        // Not paid yet: the return page shows Pending and records nothing.
        Assert.Equal(OnlinePaymentStatus.Pending, (await ResultAsync(parent, checkout.Reference)).Status);

        Api.Paystack.Pay(checkout.Reference);
        Assert.Equal(HttpStatusCode.OK, (await WebhookAsync(checkout.Reference)).StatusCode);
        var result = await ResultAsync(parent, checkout.Reference);
        Assert.Equal(OnlinePaymentStatus.Succeeded, result.Status);
        Assert.StartsWith("RCT-", result.ReceiptNumber);
        Assert.Equal(HttpStatusCode.OK, (await WebhookAsync(checkout.Reference)).StatusCode); // replayed webhook: ignored

        var detail = (await bursar.GetFromJsonAsync<InvoiceDetail>($"/api/finance/invoices/{invoice}", Json.Options))!;
        var online = Assert.Single(detail.Payments);
        Assert.Equal((60000m, PaymentMethod.Online, checkout.Reference), (online.Amount, online.Method, online.Reference));
    }

    [Fact]
    public async Task Forged_webhooks_are_rejected_and_unknown_references_ignored()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostWebhookAsync("{\"event\":\"charge.success\",\"data\":{\"reference\":\"MSS-x\"}}", "bad")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostWebhookAsync("{}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await WebhookAsync("MSS-never-started")).StatusCode);
    }

    [Fact]
    public async Task A_different_amount_is_never_applied_and_goes_to_review()
    {
        var (s, invoice, parent, bursar) = await SetUpAsync(plan: false);
        var checkout = await CheckoutAsync(await StartAsync(parent, s.Students[0], invoice, null));

        Api.Paystack.Pay(checkout.Reference, kobo: 100);
        var result = await ResultAsync(parent, checkout.Reference);

        Assert.Equal(OnlinePaymentStatus.NeedsReview, result.Status);
        Assert.Null(result.PaymentId);
        Assert.Empty((await bursar.GetFromJsonAsync<InvoiceDetail>($"/api/finance/invoices/{invoice}", Json.Options))!.Payments);
        var review = (await bursar.GetFromJsonAsync<PagedResult<OnlinePaymentRow>>("/api/finance/online-payments?status=NeedsReview&pageSize=200", Json.Options))!;
        Assert.Contains(review.Items, r => r.Reference == checkout.Reference);
    }

    [Fact]
    public async Task Money_arriving_after_the_invoice_was_settled_is_applied_up_to_the_balance_and_the_rest_flagged()
    {
        var (s, invoice, parent, bursar) = await SetUpAsync(plan: false);
        var checkout = await CheckoutAsync(await StartAsync(parent, s.Students[0], invoice, null));
        (await bursar.PostAsJsonAsync($"/api/finance/invoices/{invoice}/payments", new { amount = 100000, method = "Cash" })).EnsureSuccessStatusCode();

        Api.Paystack.Pay(checkout.Reference);
        var result = await ResultAsync(parent, checkout.Reference);

        Assert.Equal((OnlinePaymentStatus.NeedsReview, 100000m), (result.Status, result.Excess));
        Assert.Contains("refund", result.Message);
    }

    [Fact]
    public async Task Failed_charges_are_recorded_as_failed_and_other_families_get_not_found()
    {
        var (s, invoice, parent, _) = await SetUpAsync(plan: false);
        var checkout = await CheckoutAsync(await StartAsync(parent, s.Students[0], invoice, null));

        Api.Paystack.Pay(checkout.Reference, status: "failed");
        Assert.Equal(OnlinePaymentStatus.Failed, (await ResultAsync(parent, checkout.Reference)).Status);

        await (await StartAsync(parent, s.Students[1], invoice, null)).AssertProblemAsync(HttpStatusCode.NotFound);
        var stranger = await Api.ClientAsync(Roles.Parent);
        await (await stranger.GetAsync($"/api/portal/payments/{checkout.Reference}")).AssertProblemAsync(HttpStatusCode.NotFound, "payment.online_not_found");
    }

    // ---------- helpers ----------

    private static int _year = 5000 + Random.Shared.Next(0, 900);
    private static int _phone = Random.Shared.Next(1_000_000, 8_000_000);

    private async Task<(School School, Guid Invoice, HttpClient Parent, HttpClient Bursar)> SetUpAsync(bool plan)
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        var bursar = await Api.ClientAsync(Roles.Bursar);
        var year = Interlocked.Increment(ref _year);
        var session = (await (await admin.PostAsJsonAsync("/api/academics/sessions", new { name = $"{year}/{year + 1}", startsOn = $"{year}-09-14", endsOn = $"{year + 1}-07-23" }))
            .Content.ReadFromJsonAsync<SessionDto>(Json.Options))!;
        (await admin.PostAsJsonAsync($"/api/academics/sessions/{session.Id}/terms", new { number = "First", startsOn = $"{year}-09-14", endsOn = $"{year}-12-11" })).EnsureSuccessStatusCode();
        var term = (await admin.GetFromJsonAsync<List<SessionDto>>("/api/academics/sessions", Json.Options))!.Single(x => x.Id == session.Id).Terms.Single().Id;
        var level = (await admin.GetFromJsonAsync<List<ClassRoomDto>>("/api/academics/classrooms", Json.Options))!.Single(r => r.Id == s.ClassRoomId).ClassLevelId;
        if (plan)
            (await admin.PutAsJsonAsync("/api/finance/instalment-plan", new
            {
                termId = term, scope = "Term", enabled = true,
                instalments = new[] { new { percent = 60, dueOn = $"{year}-09-14" }, new { percent = 40, dueOn = $"{year}-11-14" } },
            })).EnsureSuccessStatusCode();
        (await bursar.PostAsJsonAsync("/api/finance/fee-items", new { termId = term, name = "Tuition", amount = 100000, classLevelId = level, isMandatory = true })).EnsureSuccessStatusCode();
        (await bursar.PostAsJsonAsync("/api/finance/invoices/generate", new { termId = term, classLevelId = level })).EnsureSuccessStatusCode();
        var invoice = (await bursar.GetFromJsonAsync<PagedResult<InvoiceSummary>>($"/api/finance/invoices?termId={term}&pageSize=200", Json.Options))!
            .Items.Single(i => i.StudentId == s.Students[0]).Id;

        var added = await admin.PostAsJsonAsync($"/api/students/{s.Students[0]}/guardians", new
        {
            fullName = "Mrs. Online Payer", phone = $"0802{Interlocked.Increment(ref _phone):0000000}", email = $"payer-{Guid.NewGuid():N}@example.com",
            relationship = "Mother", isPrimaryContact = true, canPickUp = true,
        });
        var guardianId = (await added.Content.ReadFromJsonAsync<GuardianAdded>(Json.Options))!.GuardianId;
        var issued = (await (await admin.PostAsync($"/api/guardians/{guardianId}/login", null)).Content.ReadFromJsonAsync<GuardianLoginIssued>(Json.Options))!;
        var first = await Api.LoginAsync(issued.Credentials!.UserName, issued.Credentials.TemporaryPassword);
        var changed = await Api.CreateClient(first.AccessToken).PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = issued.Credentials.TemporaryPassword, newPassword = "Family#NewPass2026" });
        var parent = Api.CreateClient((await changed.Content.ReadFromJsonAsync<AuthResponse>(Json.Options))!.AccessToken);
        return (s, invoice, parent, bursar);
    }

    private static Task<HttpResponseMessage> StartAsync(HttpClient parent, Guid student, Guid invoice, decimal? amount) =>
        parent.PostAsJsonAsync($"/api/portal/children/{student}/invoices/{invoice}/pay", new { amount });

    private static async Task<OnlineCheckout> CheckoutAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OnlineCheckout>(Json.Options))!;
    }

    private static async Task<OnlinePaymentResult> ResultAsync(HttpClient parent, string reference)
    {
        var response = await parent.GetAsync($"/api/portal/payments/{reference}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OnlinePaymentResult>(Json.Options))!;
    }

    private Task<HttpResponseMessage> WebhookAsync(string reference)
    {
        var body = $"{{\"event\":\"charge.success\",\"data\":{{\"reference\":\"{reference}\"}}}}";
        return PostWebhookAsync(body, FakePaymentGateway.Sign(body));
    }

    private Task<HttpResponseMessage> PostWebhookAsync(string body, string? signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/paystack") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        if (signature is not null) request.Headers.Add("x-paystack-signature", signature);
        return Api.CreateClient().SendAsync(request);
    }
}
