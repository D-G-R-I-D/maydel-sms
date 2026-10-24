using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Academics;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Finance;
using Maydel.Application.Identity;
using Maydel.Application.Notifications;
using Maydel.Application.Students;
using Maydel.Domain.Common;
using Maydel.Domain.Communication;
using Microsoft.Extensions.DependencyInjection;

namespace Maydel.Api.Tests.Finance;

/// <summary>E12.8: every payment notifies the family (portal + receipt email); overdue fees are reminded on an interval.</summary>
[Collection(nameof(ApiCollection))]
public sealed class FeeNotificationTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task A_recorded_payment_notifies_the_family_in_the_portal_and_emails_the_receipt()
    {
        var f = await SetUpAsync();

        (await f.Bursar.PostAsJsonAsync($"/api/finance/invoices/{f.Invoice}/payments", new { amount = 60000, method = "Cash" })).EnsureSuccessStatusCode();

        var inbox = await InboxAsync(f.Parent);
        Assert.Equal(1, inbox.Unread);
        var notice = Assert.Single(inbox.Items);
        Assert.Equal((NotificationKind.PaymentReceived, "/portal/fees", f.Student, false), (notice.Kind, notice.LinkUrl, notice.StudentId, notice.IsRead));
        Assert.Contains("60,000.00", notice.Body);
        Assert.Contains("Balance on invoice", notice.Body);

        await DispatchAsync();
        var mail = Assert.Single(Api.Email.Sent, m => m.To == f.Email);
        Assert.StartsWith("Payment received", mail.Subject);
        Assert.Equal("%PDF"u8.ToArray(), Assert.Single(mail.Attachments).Content[..4]);

        // Only the owner can read it; marking read clears the badge.
        await (await f.Bursar.PostAsync($"/api/notifications/{notice.Id}/read", null)).AssertProblemAsync(HttpStatusCode.NotFound, "notification.not_found");
        Assert.Equal(HttpStatusCode.NoContent, (await f.Parent.PostAsync($"/api/notifications/{notice.Id}/read", null)).StatusCode);
        Assert.Equal(0, (await InboxAsync(f.Parent)).Unread);
        await (await Api.CreateClient().GetAsync("/api/notifications")).AssertProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Overdue_fees_are_reminded_in_office_hours_once_per_interval_until_paid()
    {
        var f = await SetUpAsync();
        // 1 October: the 60% instalment (14 September) is overdue.
        var october = new DateTimeOffset(f.Year, 10, 1, 10, 0, 0, TimeSpan.FromHours(1));

        Assert.Equal(0, (await SweepAsync(october.AddHours(-3), f.Term)).Invoices); // 07:00: too early
        Assert.True((await SweepAsync(october, f.Term)).Invoices >= 1);
        var notice = Assert.Single((await InboxAsync(f.Parent)).Items);
        Assert.Equal(NotificationKind.FeesOverdue, notice.Kind);
        Assert.Contains("60,000.00", notice.Body);
        Assert.Equal(1, await RemindersToAsync(f.Email));

        await SweepAsync(october.AddDays(3), f.Term); // within the 7-day interval
        Assert.Equal(0, await RemindersToAsync(f.Email));

        // Paying the overdue instalment stops the reminders; the 40% isn't due until 14 November.
        (await f.Bursar.PostAsJsonAsync($"/api/finance/invoices/{f.Invoice}/payments", new { amount = 60000, method = "Cash" })).EnsureSuccessStatusCode();
        await SweepAsync(october.AddDays(8), f.Term);
        Assert.Equal(0, await RemindersToAsync(f.Email));
        await SweepAsync(new DateTimeOffset(f.Year, 11, 20, 9, 0, 0, TimeSpan.FromHours(1)), f.Term);
        Assert.Equal(1, await RemindersToAsync(f.Email));
    }

    [Fact]
    public async Task Admins_set_the_reminder_interval_or_switch_reminders_off()
    {
        var f = await SetUpAsync();
        var admin = await Api.ClientAsync(Roles.Admin);
        var original = (await f.Bursar.GetFromJsonAsync<FeeReminderSettings>("/api/finance/reminders", Json.Options))!;
        try
        {
            await (await admin.PutAsJsonAsync("/api/finance/reminders", new { enabled = true, intervalDays = 0 })).AssertProblemAsync(HttpStatusCode.BadRequest);
            await (await f.Bursar.PutAsJsonAsync("/api/finance/reminders", new { enabled = false, intervalDays = 7 })).AssertProblemAsync(HttpStatusCode.Forbidden);
            await (await (await Api.ClientAsync(Roles.Teacher)).GetAsync("/api/finance/reminders")).AssertProblemAsync(HttpStatusCode.Forbidden);

            (await admin.PutAsJsonAsync("/api/finance/reminders", new { enabled = false, intervalDays = 3 })).EnsureSuccessStatusCode();
            Assert.Equal(new FeeReminderSettings(false, 3), await f.Bursar.GetFromJsonAsync<FeeReminderSettings>("/api/finance/reminders", Json.Options));
            Assert.Equal(0, (await SweepAsync(new DateTimeOffset(f.Year, 10, 1, 10, 0, 0, TimeSpan.FromHours(1)), f.Term)).Invoices);
        }
        finally
        {
            (await admin.PutAsJsonAsync("/api/finance/reminders", new { enabled = original.Enabled, intervalDays = original.IntervalDays })).EnsureSuccessStatusCode();
        }
    }

    // ---------- helpers ----------

    private sealed record Family(int Year, Guid Term, Guid Student, Guid Invoice, HttpClient Parent, HttpClient Bursar, string Email);

    private static int _year = 7900 + Random.Shared.Next(0, 90);
    private static int _phone = Random.Shared.Next(1_000_000, 8_000_000);

    /// <summary>A published 100,000 invoice with a 60% / 40% plan (14 Sep, 14 Nov) and a parent with a login and an email.</summary>
    private async Task<Family> SetUpAsync()
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

        var email = $"fees-{Guid.NewGuid():N}@example.com";
        var added = await admin.PostAsJsonAsync($"/api/students/{s.Students[0]}/guardians", new
        {
            fullName = "Mr. Fee Payer", phone = $"0807{Interlocked.Increment(ref _phone):0000000}", email, relationship = "Father", isPrimaryContact = true, canPickUp = true,
        });
        var guardianId = (await added.Content.ReadFromJsonAsync<GuardianAdded>(Json.Options))!.GuardianId;
        var issued = (await (await admin.PostAsync($"/api/guardians/{guardianId}/login", null)).Content.ReadFromJsonAsync<GuardianLoginIssued>(Json.Options))!;
        var first = await Api.LoginAsync(issued.Credentials!.UserName, issued.Credentials.TemporaryPassword);
        var changed = await Api.CreateClient(first.AccessToken).PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = issued.Credentials.TemporaryPassword, newPassword = "Family#NewPass2026" });
        var parent = Api.CreateClient((await changed.Content.ReadFromJsonAsync<AuthResponse>(Json.Options))!.AccessToken);
        return new Family(year, term, s.Students[0], invoice, parent, bursar, email);
    }

    private static async Task<NotificationInbox> InboxAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<NotificationInbox>("/api/notifications", Json.Options))!;

    private async Task<FeeReminderResult> SweepAsync(DateTimeOffset now, Guid term)
    {
        await using var scope = Api.Services.CreateAsyncScope();
        var sweep = scope.ServiceProvider.GetRequiredService<ICommandHandler<SendFeeRemindersCommand, FeeReminderResult>>();
        return (await sweep.Handle(new SendFeeRemindersCommand(now, term), CancellationToken.None)).Value;
    }

    /// <summary>Sends what's queued and counts the new overdue reminders to this address.</summary>
    private async Task<int> RemindersToAsync(string email)
    {
        var before = Api.Email.Sent.Count(m => m.To == email && m.Subject.StartsWith("Fees overdue"));
        await DispatchAsync();
        return Api.Email.Sent.Count(m => m.To == email && m.Subject.StartsWith("Fees overdue")) - before;
    }

    private async Task DispatchAsync()
    {
        await using var scope = Api.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<EmailDispatcher>();
        while (await dispatcher.DispatchDueAsync(CancellationToken.None) == EmailDispatcher.BatchSize) { }
    }
}
