using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Academics;
using Maydel.Application.Common;
using Maydel.Application.Finance;
using Maydel.Application.Identity;
using Maydel.Application.Portal;
using Maydel.Application.Students;
using Maydel.Domain.Common;
using UglyToad.PdfPig;

namespace Maydel.Api.Tests.Finance;

/// <summary>Invoice and receipt PDFs (admission number prominent) and the parents' Fees tab (E10.3, E6.3).</summary>
[Collection(nameof(ApiCollection))]
public sealed class FeeDocumentTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task Invoice_and_receipt_pdfs_carry_the_admission_number_and_the_figures()
    {
        var (s, invoice, bursar, _) = await InvoicedAsync();
        var detail = (await bursar.GetFromJsonAsync<InvoiceDetail>($"/api/finance/invoices/{invoice}", Json.Options))!;

        var invoicePdf = await TextAsync(await bursar.GetAsync($"/api/finance/invoices/{invoice}/pdf"));
        Assert.Contains("INVOICE", invoicePdf);
        Assert.Contains(detail.Number, invoicePdf);
        Assert.Contains(detail.AdmissionNumber, invoicePdf);
        Assert.Contains("NGN 100,000.00", invoicePdf);

        var receipt = (await (await bursar.PostAsJsonAsync($"/api/finance/invoices/{invoice}/payments", new { amount = 100000, method = "Pos", reference = "POS-77" }))
            .Content.ReadFromJsonAsync<ReceiptDto>(Json.Options))!;
        var receiptPdf = await TextAsync(await bursar.GetAsync($"/api/finance/payments/{receipt.PaymentId}/receipt.pdf"));
        Assert.Contains(receipt.ReceiptNumber, receiptPdf);
        Assert.Contains(detail.AdmissionNumber, receiptPdf);
        Assert.Contains("POS-77", receiptPdf);
        Assert.Contains("PAID", await TextAsync(await bursar.GetAsync($"/api/finance/invoices/{invoice}/pdf")));
        await (await (await Api.ClientAsync(Roles.Teacher)).GetAsync($"/api/finance/invoices/{invoice}/pdf")).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Parents_see_their_childs_fees_and_documents_and_nobody_elses()
    {
        var (s, invoice, bursar, _) = await InvoicedAsync();
        var receipt = (await (await bursar.PostAsJsonAsync($"/api/finance/invoices/{invoice}/payments", new { amount = 100000, method = "Cash" }))
            .Content.ReadFromJsonAsync<ReceiptDto>(Json.Options))!;
        var parent = await ParentOfAsync(s.Students[0]);

        var fees = (await parent.GetFromJsonAsync<PortalFees>($"/api/portal/children/{s.Students[0]}/fees", Json.Options))!;
        var mine = Assert.Single(fees.Invoices);
        Assert.Equal((invoice, 0m), (mine.Id, fees.Balance));
        Assert.Single(mine.Payments);

        Assert.Equal(HttpStatusCode.OK, (await parent.GetAsync($"/api/portal/children/{s.Students[0]}/invoices/{invoice}/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await parent.GetAsync($"/api/portal/children/{s.Students[0]}/receipts/{receipt.PaymentId}/pdf")).StatusCode);

        // Another family's child, or this child's id paired with someone else's invoice: not found.
        await (await parent.GetAsync($"/api/portal/children/{s.Students[1]}/fees")).AssertProblemAsync(HttpStatusCode.NotFound, "portal.student_not_found");
        var other = await OtherInvoiceAsync(bursar, s.Students[1]);
        await (await parent.GetAsync($"/api/portal/children/{s.Students[0]}/invoices/{other}/pdf")).AssertProblemAsync(HttpStatusCode.NotFound, "invoice.not_found");
        await (await (await Api.ClientAsync(Roles.Bursar)).GetAsync($"/api/portal/children/{s.Students[0]}/fees")).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    // ---------- helpers ----------

    private static int _year = 4000 + Random.Shared.Next(0, 900);
    private static int _phone = Random.Shared.Next(1_000_000, 8_000_000);
    private Guid _term;

    private async Task<(School School, Guid Invoice, HttpClient Bursar, Guid Term)> InvoicedAsync()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        var bursar = await Api.ClientAsync(Roles.Bursar);
        var year = Interlocked.Increment(ref _year);
        var session = (await (await admin.PostAsJsonAsync("/api/academics/sessions", new { name = $"{year}/{year + 1}", startsOn = $"{year}-09-14", endsOn = $"{year + 1}-07-23" }))
            .Content.ReadFromJsonAsync<SessionDto>(Json.Options))!;
        (await admin.PostAsJsonAsync($"/api/academics/sessions/{session.Id}/terms", new { number = "First", startsOn = $"{year}-09-14", endsOn = $"{year}-12-11" })).EnsureSuccessStatusCode();
        _term = (await admin.GetFromJsonAsync<List<SessionDto>>("/api/academics/sessions", Json.Options))!.Single(x => x.Id == session.Id).Terms.Single().Id;
        var level = (await admin.GetFromJsonAsync<List<ClassRoomDto>>("/api/academics/classrooms", Json.Options))!.Single(r => r.Id == s.ClassRoomId).ClassLevelId;
        (await bursar.PostAsJsonAsync("/api/finance/fee-items", new { termId = _term, name = "Tuition", amount = 100000, classLevelId = level, isMandatory = true })).EnsureSuccessStatusCode();
        (await bursar.PostAsJsonAsync("/api/finance/invoices/generate", new { termId = _term, classLevelId = level })).EnsureSuccessStatusCode();
        (await bursar.PostAsJsonAsync("/api/finance/invoices/publish", new { termId = _term, classLevelId = level, notifyParents = false })).EnsureSuccessStatusCode();
        return (s, await OtherInvoiceAsync(bursar, s.Students[0]), bursar, _term);
    }

    private async Task<Guid> OtherInvoiceAsync(HttpClient bursar, Guid student) =>
        (await bursar.GetFromJsonAsync<PagedResult<InvoiceSummary>>($"/api/finance/invoices?termId={_term}&pageSize=200", Json.Options))!
            .Items.Single(i => i.StudentId == student).Id;

    private async Task<HttpClient> ParentOfAsync(Guid studentId)
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var added = await admin.PostAsJsonAsync($"/api/students/{studentId}/guardians", new
        {
            fullName = "Mrs. Portal Payer", phone = $"0803{Interlocked.Increment(ref _phone):0000000}", relationship = "Mother", isPrimaryContact = true, canPickUp = true,
        });
        added.EnsureSuccessStatusCode();
        var guardianId = (await added.Content.ReadFromJsonAsync<GuardianAdded>(Json.Options))!.GuardianId;
        var issued = (await (await admin.PostAsync($"/api/guardians/{guardianId}/login", null)).Content.ReadFromJsonAsync<GuardianLoginIssued>(Json.Options))!;
        var first = await Api.LoginAsync(issued.Credentials!.UserName, issued.Credentials.TemporaryPassword);
        var changed = await Api.CreateClient(first.AccessToken).PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = issued.Credentials.TemporaryPassword, newPassword = "Family#NewPass2026" });
        changed.EnsureSuccessStatusCode();
        return Api.CreateClient((await changed.Content.ReadFromJsonAsync<AuthResponse>(Json.Options))!.AccessToken);
    }

    private static async Task<string> TextAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        using var pdf = PdfDocument.Open(await response.Content.ReadAsByteArrayAsync());
        return string.Join("\n", pdf.GetPages().Select(p => p.Text));
    }
}
