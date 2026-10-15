using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Assessment;
using Maydel.Application.Notifications;
using Maydel.Application.Reports;
using Maydel.Domain.Common;
using Maydel.Domain.Communication;
using Microsoft.Extensions.DependencyInjection;

namespace Maydel.Api.Tests.Reports;

/// <summary>E12.3: publishing queues each guardian's report email; the dispatcher attaches the PDF and retries failures.</summary>
[Collection(nameof(ApiCollection))]
public sealed class ReportEmailTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task Publishing_queues_one_email_per_guardian_with_an_address_and_sends_the_pdf()
    {
        var (s, admin) = await ReadyClassAsync();
        var mum = Unique("mum");
        await GuardianAsync(admin, s.Students[0], mum);
        await GuardianAsync(admin, s.Students[0], null); // no email: nothing to send
        var dad = Unique("dad");
        await GuardianAsync(admin, s.Students[1], dad);

        var result = await PublishAsync(admin, s, notify: null);

        Assert.Equal(2, result.EmailsQueued);
        var queued = await EmailsAsync(admin, s);
        Assert.All(queued, e => Assert.Equal(EmailStatus.Pending, e.Status));
        Assert.Equal([dad, mum], queued.Select(e => e.ToAddress).Order());

        await DispatchAsync();

        var sent = Assert.Single(Api.Email.Sent, m => m.To == mum);
        Assert.Contains("Adaeze", sent.Subject);
        Assert.Contains("has been published", sent.TextBody);
        var pdf = Assert.Single(sent.Attachments);
        Assert.Equal("application/pdf", pdf.ContentType);
        Assert.Equal("%PDF"u8.ToArray(), pdf.Content[..4]);
        Assert.All(await EmailsAsync(admin, s), e => Assert.Equal(EmailStatus.Sent, e.Status));
    }

    [Fact]
    public async Task Admins_can_publish_without_notifying_parents()
    {
        var (s, admin) = await ReadyClassAsync();
        await GuardianAsync(admin, s.Students[0], Unique("quiet"));

        var result = await PublishAsync(admin, s, notify: false);

        Assert.Equal(0, result.EmailsQueued);
        Assert.Empty(await EmailsAsync(admin, s));
    }

    [Fact]
    public async Task Unpublished_reports_are_never_mailed_and_republishing_supersedes_unsent_emails()
    {
        var (s, admin) = await ReadyClassAsync();
        var address = Unique("later");
        await GuardianAsync(admin, s.Students[0], address);
        await PublishAsync(admin, s, notify: null);

        await UnpublishAsync(admin, s);
        await DispatchAsync();
        Assert.DoesNotContain(Api.Email.Sent, m => m.To == address);
        Assert.Equal(EmailStatus.Skipped, Assert.Single(await EmailsAsync(admin, s)).Status);

        // Published, corrected, published again before the worker ran: only the newest copy goes out.
        await PublishAsync(admin, s, notify: null);
        await UnpublishAsync(admin, s);
        await PublishAsync(admin, s, notify: null);
        var emails = await EmailsAsync(admin, s);
        Assert.Equal([EmailStatus.Pending, EmailStatus.Skipped, EmailStatus.Skipped], emails.Select(e => e.Status));

        await DispatchAsync();
        Assert.Single(Api.Email.Sent, m => m.To == address);
    }

    [Fact]
    public async Task Pressing_publish_again_sends_nothing_and_late_students_email_only_their_parents()
    {
        var (s, admin) = await ReadyClassAsync();
        var early = Unique("early");
        var late = Unique("late");
        await GuardianAsync(admin, s.Students[0], early);
        await GuardianAsync(admin, s.Students[2], late); // Chidi has no scores yet
        Assert.Equal(1, (await PublishAsync(admin, s, notify: null)).EmailsQueued);

        await (await admin.PostAsJsonAsync("/api/reports/publish", new { classRoomId = s.ClassRoomId, termId = s.TermId }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "report.already_published");

        var settings = (await admin.GetFromJsonAsync<AssessmentSettings>("/api/assessment/settings", Json.Options))!;
        var cat = settings.Components.Single(c => c.Name == "CAT" && c.IsActive).Id;
        (await admin.PutAsJsonAsync("/api/assessment/gradesheet", new
        {
            classRoomId = s.ClassRoomId, subjectId = s.SubjectId, termId = s.TermId,
            entries = new[] { new { studentId = s.Students[2], scores = new[] { new { componentId = cat, score = (decimal?)20 } } } },
        })).EnsureSuccessStatusCode();
        var second = await PublishAsync(admin, s, notify: null);

        Assert.Equal((1, 2, 1), (second.Published, second.AlreadyPublished, second.EmailsQueued));
        Assert.Equal([early, late], (await EmailsAsync(admin, s)).Select(e => e.ToAddress).Order());
    }

    [Fact]
    public async Task Failures_are_recorded_and_retried_later()
    {
        var (s, admin) = await ReadyClassAsync();
        var address = Unique("bounce");
        Api.Email.FailFor[address] = true;
        await GuardianAsync(admin, s.Students[0], address);
        await PublishAsync(admin, s, notify: null);

        await DispatchAsync();

        var email = Assert.Single(await EmailsAsync(admin, s));
        Assert.Equal((EmailStatus.Pending, 1), (email.Status, email.Attempts));
        Assert.Contains("Mailbox unavailable", email.LastError);
        await (await admin.PostAsync($"/api/reports/emails/{email.Id}/retry", null)).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "email.not_failed");
        Api.Email.FailFor.TryRemove(address, out _);
    }

    [Theory]
    [InlineData(Roles.Teacher)]
    [InlineData(Roles.Parent)]
    public async Task Only_admins_see_email_delivery(string role)
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        await (await (await Api.ClientAsync(role)).GetAsync($"/api/reports/emails?classRoomId={s.ClassRoomId}&termId={s.TermId}")).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    // ---------- helpers ----------

    private static int _phone = Random.Shared.Next(1_000_000, 8_000_000);

    private static string Unique(string name) => $"{name}-{Guid.NewGuid():N}@example.com";

    private async Task<(School School, HttpClient Admin)> ReadyClassAsync()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        var settings = (await admin.GetFromJsonAsync<AssessmentSettings>("/api/assessment/settings", Json.Options))!;
        var cat = settings.Components.Single(c => c.Name == "CAT" && c.IsActive).Id;
        (await admin.PutAsJsonAsync("/api/assessment/gradesheet", new
        {
            classRoomId = s.ClassRoomId, subjectId = s.SubjectId, termId = s.TermId,
            entries = s.Students.Take(2).Select(id => new { studentId = id, scores = new[] { new { componentId = cat, score = (decimal?)25 } } }),
        })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync("/api/reports/attendance", new
        {
            classRoomId = s.ClassRoomId, termId = s.TermId, daysOpened = 60, entries = s.Students.Select(id => new { studentId = id, daysPresent = 58 }),
        })).EnsureSuccessStatusCode();
        return (s, admin);
    }

    private static async Task GuardianAsync(HttpClient admin, Guid studentId, string? email)
    {
        var phone = $"0806{Interlocked.Increment(ref _phone):0000000}";
        (await admin.PostAsJsonAsync($"/api/students/{studentId}/guardians",
            new { fullName = "Mrs. Email Test", phone, email, relationship = "Mother", isPrimaryContact = false, canPickUp = true })).EnsureSuccessStatusCode();
    }

    private static async Task<PublishResult> PublishAsync(HttpClient admin, School s, bool? notify)
    {
        object body = notify is null ? new { classRoomId = s.ClassRoomId, termId = s.TermId } : new { classRoomId = s.ClassRoomId, termId = s.TermId, notifyParents = notify };
        var response = await admin.PostAsJsonAsync("/api/reports/publish", body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PublishResult>(Json.Options))!;
    }

    private static async Task UnpublishAsync(HttpClient admin, School s) =>
        (await admin.PostAsJsonAsync("/api/reports/unpublish", new { classRoomId = s.ClassRoomId, termId = s.TermId })).EnsureSuccessStatusCode();

    private static async Task<List<ReportEmailStatus>> EmailsAsync(HttpClient admin, School s) =>
        (await admin.GetFromJsonAsync<List<ReportEmailStatus>>($"/api/reports/emails?classRoomId={s.ClassRoomId}&termId={s.TermId}", Json.Options))!;

    private async Task DispatchAsync()
    {
        await using var scope = Api.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<EmailDispatcher>();
        while (await dispatcher.DispatchDueAsync(CancellationToken.None) == EmailDispatcher.BatchSize) { }
    }
}
