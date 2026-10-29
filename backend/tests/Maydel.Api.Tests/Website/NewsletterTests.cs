using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Newsletters;
using Maydel.Application.Notifications;
using Maydel.Domain.Common;
using Maydel.Domain.Communication;
using Microsoft.Extensions.DependencyInjection;

namespace Maydel.Api.Tests.Website;

/// <summary>E12.6: compose, test, send or schedule newsletters; unsubscribe; double opt-in; the website archive.</summary>
[Collection(nameof(ApiCollection))]
public sealed partial class NewsletterTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;
    private static int _phone = Random.Shared.Next(1_000_000, 8_000_000);

    [Fact]
    public async Task A_newsletter_to_one_class_reaches_its_parents_and_they_can_unsubscribe()
    {
        var (admin, room, email) = await ClassWithParentAsync();
        var created = await admin.PostAsJsonAsync("/api/cms/newsletters", new
        {
            title = $"Term news {Guid.NewGuid():N}", preheader = "Sports day, results and more",
            bodyHtml = "<h2>Sports day</h2><p>Join us on <strong>Friday</strong>.</p><p><a href=\"/events\" class=\"button\">See the events</a></p><script>x()</script>",
            audience = "Parents", classRoomId = room, publishOnWebsite = true,
        });
        created.EnsureSuccessStatusCode();
        var draft = (await created.Content.ReadFromJsonAsync<NewsletterDto>(Json.Options))!;
        Assert.Equal(NewsletterStatus.Draft, draft.Status);
        Assert.DoesNotContain("<script", draft.BodyHtml);
        var parents = (await admin.GetFromJsonAsync<RecipientCount>($"/api/cms/newsletters/{draft.Id}/recipients", Json.Options))!.Parents;
        Assert.True(parents >= 1);

        // A test copy first, to someone else.
        var tester = $"tester-{Guid.NewGuid():N}@example.com";
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/cms/newsletters/{draft.Id}/test", new { email = tester })).StatusCode);
        await DispatchAsync();
        var test = Assert.Single(Api.Email.Sent, m => m.To == tester);
        Assert.StartsWith("[Test] ", test.Subject);
        Assert.Null(test.Unsubscribe);

        var sent = (await (await admin.PostAsJsonAsync($"/api/cms/newsletters/{draft.Id}/send", new { })).Content.ReadFromJsonAsync<NewsletterDto>(Json.Options))!;
        Assert.Equal((NewsletterStatus.Sent, parents), (sent.Status, sent.Recipients));
        Assert.NotNull(sent.WebUrl);
        await DispatchAsync();
        var copy = Assert.Single(Api.Email.Sent, m => m.To == email);
        Assert.Contains("<h2>Sports day</h2>", copy.HtmlBody);
        Assert.Equal("Sports day, results and more", copy.Preheader);
        Assert.Contains("Unsubscribe", copy.TextBody);
        var token = Token().Match(copy.Unsubscribe!.PageUrl).Groups[1].Value;
        Assert.Equal(parents, (await admin.GetFromJsonAsync<NewsletterDto>($"/api/cms/newsletters/{draft.Id}", Json.Options))!.Delivery.Sent);

        // Sent newsletters are history: no edits; the archive page is public.
        await (await admin.PutAsJsonAsync($"/api/cms/newsletters/{draft.Id}", new { title = "x", bodyHtml = "<p>x</p>", audience = "Parents" }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "newsletter.not_editable");
        var page = await Api.CreateClient().GetFromJsonAsync<PublicNewsletter>($"/api/public{sent.WebUrl}", Json.Options);
        Assert.Contains("Sports day", page!.BodyHtml);

        // Unsubscribing (POST only) drops the parent from the next one; a bad link is a 404.
        await (await Api.CreateClient().PostAsync("/api/public/newsletter/unsubscribe?t=0000000000000000000000", null)).AssertProblemAsync(HttpStatusCode.NotFound, "newsletter.link");
        Assert.Equal(HttpStatusCode.NoContent, (await Api.CreateClient().PostAsync($"/api/public/newsletter/unsubscribe?t={token}", null)).StatusCode);
        var next = (await (await admin.PostAsJsonAsync("/api/cms/newsletters", new { title = "Next", bodyHtml = "<p>More news</p>", audience = "Parents", classRoomId = room }))
            .Content.ReadFromJsonAsync<NewsletterDto>(Json.Options))!;
        Assert.Equal(parents - 1, (await admin.GetFromJsonAsync<RecipientCount>($"/api/cms/newsletters/{next.Id}/recipients", Json.Options))!.Total);
    }

    [Fact]
    public async Task Scheduled_newsletters_go_out_when_due_and_can_be_cancelled_before()
    {
        var (admin, room, email) = await ClassWithParentAsync();
        var draft = (await (await admin.PostAsJsonAsync("/api/cms/newsletters", new { title = "Later", bodyHtml = "<p>Soon</p>", audience = "Parents", classRoomId = room }))
            .Content.ReadFromJsonAsync<NewsletterDto>(Json.Options))!;
        var at = Api.Clock.Now.AddHours(2);

        var scheduled = (await (await admin.PostAsJsonAsync($"/api/cms/newsletters/{draft.Id}/send", new { at })).Content.ReadFromJsonAsync<NewsletterDto>(Json.Options))!;
        Assert.Equal(NewsletterStatus.Scheduled, scheduled.Status);
        var cancelled = (await (await admin.PostAsync($"/api/cms/newsletters/{draft.Id}/cancel", null)).Content.ReadFromJsonAsync<NewsletterDto>(Json.Options))!;
        Assert.Equal(NewsletterStatus.Draft, cancelled.Status);
        (await admin.PostAsJsonAsync($"/api/cms/newsletters/{draft.Id}/send", new { at })).EnsureSuccessStatusCode();

        Assert.Equal(0, await SendDueAsync(Api.Clock.Now.AddHours(1)));
        Assert.True(await SendDueAsync(Api.Clock.Now.AddHours(3)) >= 1);
        Assert.Equal(NewsletterStatus.Sent, (await admin.GetFromJsonAsync<NewsletterDto>($"/api/cms/newsletters/{draft.Id}", Json.Options))!.Status);
        await DispatchAsync();
        Assert.Single(Api.Email.Sent, m => m.To == email && m.Subject == "Later");
    }

    [Fact]
    public async Task Website_sign_ups_must_confirm_before_they_get_newsletters()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var email = $"fan-{Guid.NewGuid():N}@example.com";
        Assert.Equal(HttpStatusCode.NoContent, (await Api.CreateClient().PostAsJsonAsync("/api/public/site/subscribe", new { email })).StatusCode);
        var news = (await (await admin.PostAsJsonAsync("/api/cms/newsletters", new { title = "For fans", bodyHtml = "<p>Hello</p>", audience = "Subscribers" }))
            .Content.ReadFromJsonAsync<NewsletterDto>(Json.Options))!;
        async Task<int> SubscribersAsync() => (await admin.GetFromJsonAsync<RecipientCount>($"/api/cms/newsletters/{news.Id}/recipients", Json.Options))!.Subscribers;
        var before = await SubscribersAsync();

        await DispatchAsync();
        var confirm = Assert.Single(Api.Email.Sent, m => m.To == email);
        Assert.StartsWith("Please confirm your subscription", confirm.Subject);
        var token = Token().Match(confirm.TextBody).Groups[1].Value;

        await (await Api.CreateClient().PostAsync("/api/public/newsletter/confirm?t=ffffffffffffffffffffffff", null)).AssertProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal(HttpStatusCode.NoContent, (await Api.CreateClient().PostAsync($"/api/public/newsletter/confirm?t={token}", null)).StatusCode);
        Assert.Equal(before + 1, await SubscribersAsync());
        await (await (await Api.ClientAsync(Roles.Teacher)).GetAsync("/api/cms/newsletters")).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    // ---------- helpers ----------

    [GeneratedRegex(@"[?&]t=([0-9a-f]{16,64})")]
    private static partial Regex Token();

    private async Task<(HttpClient Admin, Guid Room, string Email)> ClassWithParentAsync()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        var email = $"news-{Guid.NewGuid():N}@example.com";
        (await admin.PostAsJsonAsync($"/api/students/{s.Students[0]}/guardians", new
        {
            fullName = "Mrs. News Reader", phone = $"0806{Interlocked.Increment(ref _phone):0000000}", email, relationship = "Mother", isPrimaryContact = true, canPickUp = true,
        })).EnsureSuccessStatusCode();
        return (admin, s.ClassRoomId, email);
    }

    private async Task<int> SendDueAsync(DateTimeOffset now)
    {
        await using var scope = Api.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<ICommandHandler<SendDueNewslettersCommand, int>>().Handle(new SendDueNewslettersCommand(now), CancellationToken.None)).Value;
    }

    private async Task DispatchAsync()
    {
        await using var scope = Api.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<EmailDispatcher>();
        while (await dispatcher.DispatchDueAsync(CancellationToken.None) == EmailDispatcher.BatchSize) { }
    }
}
