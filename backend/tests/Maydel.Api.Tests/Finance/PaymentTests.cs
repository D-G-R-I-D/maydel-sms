using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Academics;
using Maydel.Application.Common;
using Maydel.Application.Finance;
using Maydel.Domain.Common;
using Maydel.Domain.Finance;

namespace Maydel.Api.Tests.Finance;

/// <summary>E10.3–E10.4: payments and receipts, reversals, cancellation, adjustments, statements and the debtors list.</summary>
[Collection(nameof(ApiCollection))]
public sealed class PaymentTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task Without_instalments_the_full_balance_is_paid_and_a_numbered_receipt_issued()
    {
        var (s, invoice, bursar, _) = await InvoicedAsync(plan: false);

        var partial = await PayAsync(bursar, invoice, 50000);
        Assert.True((await partial.AssertProblemAsync(HttpStatusCode.BadRequest, "validation")).GetProperty("errors").TryGetProperty("amount", out _));
        await (await PayAsync(bursar, invoice, 100000.01m)).AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        await (await bursar.PostAsJsonAsync($"/api/finance/invoices/{invoice}/payments", new { amount = 100000, method = "Online" }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation");

        var receipt = await ReceiptAsync(await PayAsync(bursar, invoice, 100000, "TRF-998"));

        Assert.StartsWith("RCT-", receipt.ReceiptNumber);
        Assert.Equal((0m, "TRF-998", s.Students[0]), (receipt.BalanceAfter, receipt.Reference, (await DetailAsync(bursar, invoice)).StudentId));
        Assert.Equal(InvoiceStatus.Paid, (await DetailAsync(bursar, invoice)).Status);
        Assert.False(string.IsNullOrEmpty(receipt.AdmissionNumber));
    }

    [Fact]
    public async Task With_instalments_part_payments_show_what_is_overdue_on_the_debtors_list()
    {
        var (s, invoice, bursar, term) = await InvoicedAsync(plan: true); // 60% due 14 Sep 2026 (past), 40% due 2099

        var unpaid = await DetailAsync(bursar, invoice);
        Assert.Equal((60000m, 60000m, 60000m, true), (unpaid.DueNow, unpaid.Overdue, unpaid.MinimumPayment, unpaid.InstalmentsAllowed));
        var debtors = (await bursar.GetFromJsonAsync<DebtorsReport>($"/api/finance/debtors?termId={term}&classRoomId={s.ClassRoomId}", Json.Options))!;
        var row = Assert.Single(debtors.Rows, r => r.InvoiceId == invoice);
        Assert.Equal((100000m, 60000m, "Mrs. Fee Payer"), (row.Balance, row.Overdue, row.Guardian));

        // Each payment completes an instalment: at least 60% first, then at least the remaining 40%.
        await (await PayAsync(bursar, invoice, 50000)).AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        (await PayAsync(bursar, invoice, 70000)).EnsureSuccessStatusCode();
        var detail = await DetailAsync(bursar, invoice);
        Assert.Equal((InvoiceStatus.PartiallyPaid, 0m, 30000m), (detail.Status, detail.Overdue, detail.MinimumPayment));
        await (await PayAsync(bursar, invoice, 20000)).AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        Assert.Equal(new DateOnly(2099, 1, 15), row.NextDueOn);

        var csv = await bursar.GetAsync($"/api/finance/debtors.csv?termId={term}&classRoomId={s.ClassRoomId}");
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
        var text = await csv.Content.ReadAsStringAsync();
        Assert.StartsWith("﻿Admission number,Student", text);
        Assert.Contains(row.AdmissionNumber, text);
    }

    [Fact]
    public async Task Only_admins_reverse_payments_and_a_reversal_is_a_new_entry_once()
    {
        var (_, invoice, bursar, _) = await InvoicedAsync(plan: false);
        var admin = await Api.ClientAsync(Roles.Admin);
        var paid = await ReceiptAsync(await PayAsync(bursar, invoice, 100000));

        await (await bursar.PostAsJsonAsync($"/api/finance/payments/{paid.PaymentId}/reverse", new { reason = "Mistake" })).AssertProblemAsync(HttpStatusCode.Forbidden);
        var reversal = await ReceiptAsync(await admin.PostAsJsonAsync($"/api/finance/payments/{paid.PaymentId}/reverse", new { reason = "Keyed against the wrong child" }));

        Assert.Equal((-100000m, true, 100000m), (reversal.Amount, reversal.IsReversal, reversal.BalanceAfter));
        Assert.NotEqual(paid.ReceiptNumber, reversal.ReceiptNumber);
        var detail = await DetailAsync(bursar, invoice);
        Assert.Equal(reversal.ReceiptNumber, detail.Payments.Single(p => p.Id == paid.PaymentId).ReversedBy);
        Assert.Equal(InvoiceStatus.Unpaid, detail.Status);
        await (await admin.PostAsJsonAsync($"/api/finance/payments/{paid.PaymentId}/reverse", new { reason = "Again" }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "payment.already_reversed");
    }

    [Fact]
    public async Task Cancelling_needs_no_money_on_the_invoice_and_frees_the_student_to_be_invoiced_again()
    {
        var (s, invoice, bursar, term) = await InvoicedAsync(plan: false);
        var admin = await Api.ClientAsync(Roles.Admin);
        var paid = await ReceiptAsync(await PayAsync(bursar, invoice, 100000));

        await (await admin.PostAsJsonAsync($"/api/finance/invoices/{invoice}/cancel", new { reason = "Left school" }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "invoice.has_payments");
        (await admin.PostAsJsonAsync($"/api/finance/payments/{paid.PaymentId}/reverse", new { reason = "Refunded" })).EnsureSuccessStatusCode();
        var cancelled = await admin.PostAsJsonAsync($"/api/finance/invoices/{invoice}/cancel", new { reason = "Left school" });

        Assert.Equal(InvoiceStatus.Cancelled, (await cancelled.Content.ReadFromJsonAsync<InvoiceDetail>(Json.Options))!.Status);
        var level = (await admin.GetFromJsonAsync<List<ClassRoomDto>>("/api/academics/classrooms", Json.Options))!.Single(r => r.Id == s.ClassRoomId).ClassLevelId;
        (await bursar.PostAsJsonAsync("/api/finance/invoices/generate", new { termId = term, classLevelId = level })).EnsureSuccessStatusCode();
        var page = (await bursar.GetFromJsonAsync<PagedResult<InvoiceSummary>>($"/api/finance/invoices?termId={term}&pageSize=200", Json.Options))!;
        Assert.Equal(2, page.Items.Count(i => i.StudentId == s.Students[0]));
    }

    [Fact]
    public async Task Scholarships_reduce_the_invoice_and_the_statement_keeps_a_running_balance()
    {
        var (s, invoice, bursar, _) = await InvoicedAsync(plan: true);
        var admin = await Api.ClientAsync(Roles.Admin);
        (await PayAsync(bursar, invoice, 60000)).EnsureSuccessStatusCode();

        var adjusted = await admin.PostAsJsonAsync($"/api/finance/invoices/{invoice}/adjustments", new { description = "Scholarship (25%)", amount = -25000 });
        Assert.Equal(75000m, (await adjusted.Content.ReadFromJsonAsync<InvoiceDetail>(Json.Options))!.Total);
        await (await admin.PostAsJsonAsync($"/api/finance/invoices/{invoice}/adjustments", new { description = "Too much", amount = -60000 }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        await (await bursar.PostAsJsonAsync($"/api/finance/invoices/{invoice}/adjustments", new { description = "x", amount = -1 })).AssertProblemAsync(HttpStatusCode.Forbidden);

        var statement = (await bursar.GetFromJsonAsync<StudentStatement>($"/api/finance/students/{s.Students[0]}/statement", Json.Options))!;
        Assert.Equal((75000m, 60000m, 15000m), (statement.TotalBilled, statement.TotalPaid, statement.Balance));
        Assert.Equal(["Invoice", "Payment"], statement.Entries.Select(e => e.Kind));
        Assert.Equal(15000m, statement.Entries[^1].Balance);
    }

    [Theory]
    [InlineData(Roles.Teacher)]
    [InlineData(Roles.Parent)]
    public async Task Only_finance_staff_take_payments(string role)
    {
        var (_, invoice, _, _) = await InvoicedAsync(plan: false);
        await (await PayAsync(await Api.ClientAsync(role), invoice, 1)).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    // ---------- helpers ----------

    private static int _year = 3000 + Random.Shared.Next(0, 900);
    private static int _phone = Random.Shared.Next(1_000_000, 8_000_000);

    /// <summary>
    /// A fresh session/term, a ₦100,000 tuition item, invoices for the class's level, and Adaeze's invoice id.
    /// With <paramref name="plan"/>: 60% due 14 Sep 2026 (already due, school "today" is 15 Oct 2026) and 40% in 2099.
    /// </summary>
    private async Task<(School School, Guid Invoice, HttpClient Bursar, Guid Term)> InvoicedAsync(bool plan)
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
                instalments = new[] { new { percent = 60, dueOn = "2026-09-14" }, new { percent = 40, dueOn = "2099-01-15" } },
            })).EnsureSuccessStatusCode();
        (await bursar.PostAsJsonAsync("/api/finance/fee-items", new { termId = term, name = "Tuition", amount = 100000, classLevelId = level, isMandatory = true })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/students/{s.Students[0]}/guardians", new
        {
            fullName = "Mrs. Fee Payer", phone = $"0804{Interlocked.Increment(ref _phone):0000000}", relationship = "Mother", isPrimaryContact = true, canPickUp = true,
        })).EnsureSuccessStatusCode();
        (await bursar.PostAsJsonAsync("/api/finance/invoices/generate", new { termId = term, classLevelId = level })).EnsureSuccessStatusCode();
        var page = (await bursar.GetFromJsonAsync<PagedResult<InvoiceSummary>>($"/api/finance/invoices?termId={term}&pageSize=200", Json.Options))!;
        return (s, page.Items.Single(i => i.StudentId == s.Students[0]).Id, bursar, term);
    }

    private static Task<HttpResponseMessage> PayAsync(HttpClient client, Guid invoice, decimal amount, string? reference = null) =>
        client.PostAsJsonAsync($"/api/finance/invoices/{invoice}/payments", new { amount, method = "BankTransfer", reference });

    private static async Task<ReceiptDto> ReceiptAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReceiptDto>(Json.Options))!;
    }

    private static async Task<InvoiceDetail> DetailAsync(HttpClient client, Guid invoice) =>
        (await client.GetFromJsonAsync<InvoiceDetail>($"/api/finance/invoices/{invoice}", Json.Options))!;
}
