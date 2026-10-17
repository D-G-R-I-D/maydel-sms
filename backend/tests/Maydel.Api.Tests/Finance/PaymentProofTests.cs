using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Academics;
using Maydel.Application.Common;
using Maydel.Application.Finance;
using Maydel.Application.Identity;
using Maydel.Application.Notifications;
using Maydel.Application.Students;
using Maydel.Domain.Common;
using Maydel.Domain.Finance;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Maydel.Api.Tests.Finance;

/// <summary>E10.8: parents upload proof of a bank payment; the bursar confirms (payment + receipt) or rejects (reason).</summary>
[Collection(nameof(ApiCollection))]
public sealed class PaymentProofTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task Bursar_confirms_a_proof_which_records_the_payment_and_emails_the_receipt()
    {
        var (s, invoice, parent, bursar, email) = await SetUpAsync();

        var sent = await ProofAsync(await SubmitAsync(parent, s.Students[0], invoice, 40000, Jpeg()));
        Assert.Equal((ProofStatus.Pending, "image/jpeg"), (sent.Status, sent.ContentType));
        Assert.Empty((await DetailAsync(bursar, invoice)).Payments); // nothing recorded yet

        var queue = (await bursar.GetFromJsonAsync<PagedResult<ProofDto>>("/api/finance/proofs?pageSize=200", Json.Options))!;
        Assert.Contains(queue.Items, p => p.Id == sent.Id);
        var file = await bursar.GetAsync($"/api/finance/proofs/{sent.Id}/file");
        Assert.Equal("image/jpeg", file.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", file.Content.Headers.ContentDisposition?.DispositionType);

        var confirmed = await ProofAsync(await bursar.PostAsJsonAsync($"/api/finance/proofs/{sent.Id}/confirm", new { }));
        Assert.Equal(ProofStatus.Confirmed, confirmed.Status);
        Assert.StartsWith("RCT-", confirmed.ReceiptNumber);
        var payment = Assert.Single((await DetailAsync(bursar, invoice)).Payments);
        Assert.Equal((40000m, PaymentMethod.BankTransfer, "TRF-889"), (payment.Amount, payment.Method, payment.Reference)); // below the 60% minimum is fine: the money is in

        await (await bursar.PostAsJsonAsync($"/api/finance/proofs/{sent.Id}/confirm", new { })).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "proof.already_reviewed");
        await DispatchAsync();
        var mail = Assert.Single(Api.Email.Sent, m => m.To == email);
        Assert.Contains(confirmed.ReceiptNumber!, (await DetailAsync(bursar, invoice)).Payments.Single().ReceiptNumber);
        Assert.Equal("%PDF"u8.ToArray(), Assert.Single(mail.Attachments).Content[..4]);
    }

    [Fact]
    public async Task Rejected_proofs_record_nothing_and_tell_the_parent_why()
    {
        var (s, invoice, parent, bursar, email) = await SetUpAsync();
        var sent = await ProofAsync(await SubmitAsync(parent, s.Students[0], invoice, 50000, Jpeg()));

        await (await bursar.PostAsJsonAsync($"/api/finance/proofs/{sent.Id}/reject", new { reason = " " })).AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        var rejected = await ProofAsync(await bursar.PostAsJsonAsync($"/api/finance/proofs/{sent.Id}/reject", new { reason = "Teller is unreadable" }));

        Assert.Equal((ProofStatus.Rejected, "Teller is unreadable"), (rejected.Status, rejected.RejectReason));
        Assert.Empty((await DetailAsync(bursar, invoice)).Payments);
        var mine = (await parent.GetFromJsonAsync<List<ProofDto>>($"/api/portal/children/{s.Students[0]}/proofs", Json.Options))!;
        Assert.Equal(ProofStatus.Rejected, Assert.Single(mine).Status);
        await DispatchAsync();
        Assert.Contains("Teller is unreadable", Assert.Single(Api.Email.Sent, m => m.To == email).TextBody);
    }

    [Fact]
    public async Task Proofs_are_checked_files_amounts_and_families()
    {
        var (s, invoice, parent, _, _) = await SetUpAsync();

        await (await SubmitAsync(parent, s.Students[0], invoice, 50000, "<script>alert(1)</script>"u8.ToArray())).AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        await (await SubmitAsync(parent, s.Students[0], invoice, 150000, Jpeg())).AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync(parent, s.Students[0], invoice, 1000, "%PDF-1.4\n%fake teller\n"u8.ToArray())).StatusCode);
        await (await SubmitAsync(parent, s.Students[1], invoice, 1000, Jpeg())).AssertProblemAsync(HttpStatusCode.NotFound);
        await (await (await Api.ClientAsync(Roles.Teacher)).GetAsync("/api/finance/proofs")).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    // ---------- helpers ----------

    private static int _year = 7000 + Random.Shared.Next(0, 900);
    private static int _phone = Random.Shared.Next(1_000_000, 8_000_000);

    private async Task<(School School, Guid Invoice, HttpClient Parent, HttpClient Bursar, string Email)> SetUpAsync()
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
        (await admin.PutAsJsonAsync("/api/finance/instalment-plan", new
        {
            termId = term, scope = "Term", enabled = true,
            instalments = new[] { new { percent = 60, dueOn = $"{year}-09-14" }, new { percent = 40, dueOn = $"{year}-11-14" } },
        })).EnsureSuccessStatusCode();
        (await bursar.PostAsJsonAsync("/api/finance/fee-items", new { termId = term, name = "Tuition", amount = 100000, classLevelId = level, isMandatory = true })).EnsureSuccessStatusCode();
        (await bursar.PostAsJsonAsync("/api/finance/invoices/generate", new { termId = term, classLevelId = level })).EnsureSuccessStatusCode();
        (await bursar.PostAsJsonAsync("/api/finance/invoices/publish", new { termId = term, classLevelId = level, notifyParents = false })).EnsureSuccessStatusCode();
        var invoice = (await bursar.GetFromJsonAsync<PagedResult<InvoiceSummary>>($"/api/finance/invoices?termId={term}&pageSize=200", Json.Options))!
            .Items.Single(i => i.StudentId == s.Students[0]).Id;

        var email = $"proof-{Guid.NewGuid():N}@example.com";
        var added = await admin.PostAsJsonAsync($"/api/students/{s.Students[0]}/guardians", new
        {
            fullName = "Mrs. Bank Payer", phone = $"0809{Interlocked.Increment(ref _phone):0000000}", email, relationship = "Mother", isPrimaryContact = true, canPickUp = true,
        });
        var guardianId = (await added.Content.ReadFromJsonAsync<GuardianAdded>(Json.Options))!.GuardianId;
        var issued = (await (await admin.PostAsync($"/api/guardians/{guardianId}/login", null)).Content.ReadFromJsonAsync<GuardianLoginIssued>(Json.Options))!;
        // With an email on record the parent signs in with it; LinkedExistingAccount is false for a fresh address.
        var first = await Api.LoginAsync(issued.Credentials!.UserName, issued.Credentials.TemporaryPassword);
        var changed = await Api.CreateClient(first.AccessToken).PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = issued.Credentials.TemporaryPassword, newPassword = "Family#NewPass2026" });
        var parent = Api.CreateClient((await changed.Content.ReadFromJsonAsync<AuthResponse>(Json.Options))!.AccessToken);
        return (s, invoice, parent, bursar, email);
    }

    private static Task<HttpResponseMessage> SubmitAsync(HttpClient parent, Guid student, Guid invoice, decimal amount, byte[] file)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(amount.ToString(System.Globalization.CultureInfo.InvariantCulture)), "amount" },
            { new StringContent("2026-10-14"), "paidOn" },
            { new StringContent("TRF-889"), "bankReference" },
        };
        var content = new ByteArrayContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(content, "file", "teller");
        return parent.PostAsync($"/api/portal/children/{student}/invoices/{invoice}/proofs", form);
    }

    private static async Task<ProofDto> ProofAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProofDto>(Json.Options))!;
    }

    private static async Task<InvoiceDetail> DetailAsync(HttpClient bursar, Guid invoice) =>
        (await bursar.GetFromJsonAsync<InvoiceDetail>($"/api/finance/invoices/{invoice}", Json.Options))!;

    private async Task DispatchAsync()
    {
        await using var scope = Api.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<EmailDispatcher>();
        while (await dispatcher.DispatchDueAsync(CancellationToken.None) == EmailDispatcher.BatchSize) { }
    }

    private static byte[] Jpeg()
    {
        using var surface = SKSurface.Create(new SKImageInfo(400, 300));
        surface.Canvas.Clear(SKColors.LightGray);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
        return data.ToArray();
    }
}
