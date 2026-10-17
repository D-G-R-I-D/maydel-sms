using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Academics;
using Maydel.Application.Common;
using Maydel.Application.Finance;
using Maydel.Application.Notifications;
using Maydel.Application.Students;
using Maydel.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Maydel.Api.Tests.Finance;

/// <summary>E10.6 new vs returning fee items, E10.7 draft → publish with parent emails.</summary>
[Collection(nameof(ApiCollection))]
public sealed class InvoicePublishTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task New_intake_and_returning_students_get_their_own_items()
    {
        // Admission dates can't be far in the future, so this uses the real first term of 2026/2027 (starts 14 Sep 2026).
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        var bursar = await Api.ClientAsync(Roles.Bursar);
        var term = (await admin.GetFromJsonAsync<List<SessionDto>>("/api/academics/sessions", Json.Options))!
            .Single(x => x.Name == "2026/2027").Terms.Single(t => t.Number == Domain.Academics.TermNumber.First).Id;
        var level = (await admin.GetFromJsonAsync<List<ClassRoomDto>>("/api/academics/classrooms", Json.Options))!.Single(r => r.Id == s.ClassRoomId).ClassLevelId;
        var newcomer = await StudentAsync(admin, s.ClassRoomId, "2026-08-20");   // summer admission: new intake
        var returning = await StudentAsync(admin, s.ClassRoomId, "2025-09-08");  // last session's intake
        var tag = Guid.NewGuid().ToString("N")[..6];
        await ItemAsync(bursar, term, level, $"Tuition {tag}", 100000, "All");
        await ItemAsync(bursar, term, level, $"Acceptance fee {tag}", 30000, "NewIntake");
        await ItemAsync(bursar, term, level, $"Re-registration {tag}", 5000, "Returning");

        (await bursar.PostAsJsonAsync("/api/finance/invoices/generate", new { termId = term, classLevelId = level })).EnsureSuccessStatusCode();

        var newInvoice = await InvoiceAsync(bursar, term, newcomer);
        Assert.True(newInvoice.NewIntake);
        Assert.Contains(newInvoice.Lines, l => l.Description == $"Acceptance fee {tag}");
        Assert.DoesNotContain(newInvoice.Lines, l => l.Description == $"Re-registration {tag}");
        var old = await InvoiceAsync(bursar, term, returning);
        Assert.False(old.NewIntake);
        Assert.Contains(old.Lines, l => l.Description == $"Re-registration {tag}");
        Assert.DoesNotContain(old.Lines, l => l.Description == $"Acceptance fee {tag}");
        var newOnly = (await bursar.GetFromJsonAsync<PagedResult<InvoiceSummary>>($"/api/finance/invoices?termId={term}&newIntake=true&pageSize=200", Json.Options))!;
        Assert.Contains(newOnly.Items, i => i.StudentId == newcomer);
        Assert.DoesNotContain(newOnly.Items, i => i.StudentId == returning);
    }

    [Fact]
    public async Task Invoices_are_drafts_until_published_then_parents_are_emailed_once_with_the_pdf()
    {
        var (s, admin, bursar, _, term, level) = await SetUpAsync();
        var email = $"fees-{Guid.NewGuid():N}@example.com";
        (await admin.PostAsJsonAsync($"/api/students/{s.Students[0]}/guardians", new
        {
            fullName = "Mrs. Draft Watcher", phone = $"0801{Random.Shared.Next(1_000_000, 9_999_999)}", email, relationship = "Mother", isPrimaryContact = true, canPickUp = true,
        })).EnsureSuccessStatusCode();
        await ItemAsync(bursar, term, level, "Tuition", 100000, "All");
        (await bursar.PostAsJsonAsync("/api/finance/invoices/generate", new { termId = term, classLevelId = level })).EnsureSuccessStatusCode();

        var draft = await InvoiceAsync(bursar, term, s.Students[0]);
        Assert.False(draft.IsPublished);
        var drafts = (await bursar.GetFromJsonAsync<PagedResult<InvoiceSummary>>($"/api/finance/invoices?termId={term}&published=false&pageSize=200", Json.Options))!;
        Assert.Contains(drafts.Items, i => i.Id == draft.Id);

        var first = await PublishAsync(bursar, term, level);
        Assert.True(first.Published >= 3);
        Assert.True(first.EmailsQueued >= 1);
        Assert.True((await InvoiceAsync(bursar, term, s.Students[0])).IsPublished);

        var again = await PublishAsync(bursar, term, level);
        Assert.Equal(0, again.Published);
        Assert.Equal(0, again.EmailsQueued);

        await using (var scope = Api.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<EmailDispatcher>();
            while (await dispatcher.DispatchDueAsync(CancellationToken.None) == EmailDispatcher.BatchSize) { }
        }
        var sent = Assert.Single(Api.Email.Sent, m => m.To == email);
        Assert.Contains(draft.AdmissionNumber, sent.TextBody);
        Assert.Equal("%PDF"u8.ToArray(), Assert.Single(sent.Attachments).Content[..4]);
    }

    [Fact]
    public async Task On_by_default_items_bill_everyone_except_students_unticked()
    {
        var (s, _, bursar, _, term, level) = await SetUpAsync();
        var created = await bursar.PostAsJsonAsync("/api/finance/fee-items", new
        {
            termId = term, name = "Books", amount = 15000, classLevelId = level, isMandatory = false, chargedByDefault = true,
        });
        var books = (await created.Content.ReadFromJsonAsync<FeeItemDto>(Json.Options))!;
        var takers = (await bursar.GetFromJsonAsync<List<OptInStudent>>($"/api/finance/fee-items/{books.Id}/opt-ins", Json.Options))!;
        Assert.All(takers, t => Assert.True(t.OptedIn)); // everyone by default

        // Bola brings her own books: untick her by sending everyone else.
        var keep = takers.Where(t => t.StudentId != s.Students[1]).Select(t => t.StudentId);
        var after = await bursar.PutAsJsonAsync($"/api/finance/fee-items/{books.Id}/opt-ins", new { studentIds = keep });
        Assert.False((await after.Content.ReadFromJsonAsync<List<OptInStudent>>(Json.Options))!.Single(t => t.StudentId == s.Students[1]).OptedIn);
        Assert.Equal(1, (await bursar.GetFromJsonAsync<List<FeeItemDto>>($"/api/finance/fee-items?termId={term}", Json.Options))!.Single().OptedOut);

        (await bursar.PostAsJsonAsync("/api/finance/invoices/generate", new { termId = term, classLevelId = level })).EnsureSuccessStatusCode();
        Assert.Equal(15000m, (await InvoiceAsync(bursar, term, s.Students[0])).Total);
        Assert.Equal(0m, (await InvoiceAsync(bursar, term, s.Students[2])).Total - 15000m);
        var page = (await bursar.GetFromJsonAsync<PagedResult<InvoiceSummary>>($"/api/finance/invoices?termId={term}&pageSize=200", Json.Options))!;
        Assert.DoesNotContain(page.Items, i => i.StudentId == s.Students[1]); // nothing to bill
    }

    [Fact]
    public async Task Optional_new_intake_items_are_on_by_default_unless_said_otherwise()
    {
        var (_, _, bursar, _, term, level) = await SetUpAsync();
        var uniform = await (await bursar.PostAsJsonAsync("/api/finance/fee-items", new { termId = term, name = "Uniform pack", amount = 25000, classLevelId = level, isMandatory = false, audience = "NewIntake" }))
            .Content.ReadFromJsonAsync<FeeItemDto>(Json.Options);
        var bus = await (await bursar.PostAsJsonAsync("/api/finance/fee-items", new { termId = term, name = "Bus", amount = 50000, classLevelId = level, isMandatory = false }))
            .Content.ReadFromJsonAsync<FeeItemDto>(Json.Options);
        Assert.True(uniform!.ChargedByDefault);
        Assert.False(bus!.ChargedByDefault);
    }

    [Fact]
    public async Task Teachers_cannot_publish_invoices()
    {
        var (_, _, _, _, term, level) = await SetUpAsync();
        await (await (await Api.ClientAsync(Roles.Teacher)).PostAsJsonAsync("/api/finance/invoices/publish", new { termId = term, classLevelId = level }))
            .AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    // ---------- helpers ----------

    private static int _year = 6000 + Random.Shared.Next(0, 900);

    private async Task<(School School, HttpClient Admin, HttpClient Bursar, int Year, Guid Term, Guid Level)> SetUpAsync()
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
        return (s, admin, bursar, year, term, level);
    }

    private static async Task<Guid> StudentAsync(HttpClient admin, Guid classRoomId, string admittedOn)
    {
        var response = await admin.PostAsJsonAsync("/api/students", new { firstName = "Nkem", lastName = "Newcomer", dateOfBirth = "2019-02-02", classRoomId, admittedOn });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<StudentDetail>(Json.Options))!.Id;
    }

    private static async Task ItemAsync(HttpClient bursar, Guid term, Guid level, string name, decimal amount, string audience) =>
        (await bursar.PostAsJsonAsync("/api/finance/fee-items", new { termId = term, name, amount, classLevelId = level, isMandatory = true, audience })).EnsureSuccessStatusCode();

    private static async Task<PublishInvoicesResult> PublishAsync(HttpClient bursar, Guid term, Guid level)
    {
        var response = await bursar.PostAsJsonAsync("/api/finance/invoices/publish", new { termId = term, classLevelId = level });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PublishInvoicesResult>(Json.Options))!;
    }

    private static async Task<InvoiceDetail> InvoiceAsync(HttpClient bursar, Guid term, Guid student)
    {
        var page = (await bursar.GetFromJsonAsync<PagedResult<InvoiceSummary>>($"/api/finance/invoices?termId={term}&pageSize=200", Json.Options))!;
        var id = page.Items.Single(i => i.StudentId == student).Id;
        return (await bursar.GetFromJsonAsync<InvoiceDetail>($"/api/finance/invoices/{id}", Json.Options))!;
    }
}
