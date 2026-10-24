using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Admissions;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Common;
using Maydel.Application.Website.Cms;
using Maydel.Application.Website.Public;
using Maydel.Domain.Common;

namespace Maydel.Api.Tests.Website;

/// <summary>E7.1 extras for the richer website: term calendar, live stats, newsletter, testimonials.</summary>
[Collection(nameof(ApiCollection))]
public sealed class WebsiteExtrasTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task Calendar_stats_newsletter_and_testimonials()
    {
        var site = Api.CreateClient();
        var admin = await Api.ClientAsync(Roles.Admin);

        var calendar = await AdmissionsKit.ReadAsync<List<CalendarSession>>(await site.GetAsync("/api/public/site/calendar"));
        var current = Assert.Single(calendar, s => s.IsCurrent);
        Assert.NotEmpty(current.Terms);

        var stats = await AdmissionsKit.ReadAsync<PublicStats>(await site.GetAsync("/api/public/site/stats"));
        Assert.True(stats.Shown);
        Assert.True(stats.Levels > 0);
        Assert.StartsWith(stats.Students.ToString(), stats.StudentsLabel);

        var email = $"news-{Guid.NewGuid():N}@example.com";
        Assert.Equal(HttpStatusCode.NoContent, (await site.PostAsJsonAsync("/api/public/site/subscribe", new { email = email.ToUpperInvariant(), website = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await site.PostAsJsonAsync("/api/public/site/subscribe", new { email, website = "" })).StatusCode); // twice is fine
        await (await site.PostAsJsonAsync("/api/public/site/subscribe", new { email = "nope", website = "" })).AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        var list = await AdmissionsKit.ReadAsync<PagedResult<SubscriberDto>>(await admin.GetAsync("/api/cms/subscribers?pageSize=200"));
        var me = Assert.Single(list.Items, s => s.Email == email);
        Assert.False(me.Confirmed); // double opt-in: waits for the link in the confirmation email (NewsletterTests)
        Assert.DoesNotContain(email, await admin.GetStringAsync("/api/cms/subscribers.csv")); // only confirmed, active subscribers
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/cms/subscribers/{me.Id}")).StatusCode);

        var tag = Guid.NewGuid().ToString("N")[..6];
        var shown = await AdmissionsKit.ReadAsync<TestimonialDto>(await admin.PostAsJsonAsync("/api/cms/testimonials",
            new { quote = $"Our son loves it {tag}", attribution = "Parent of a Year 3 pupil", order = 1, isPublished = true }));
        var hidden = await AdmissionsKit.ReadAsync<TestimonialDto>(await admin.PostAsJsonAsync("/api/cms/testimonials",
            new { quote = $"Draft {tag}", attribution = "Parent", order = 2, isPublished = false }));
        var published = await AdmissionsKit.ReadAsync<List<PublicTestimonial>>(await site.GetAsync("/api/public/site/testimonials"));
        Assert.Contains(published, t => t.Id == shown.Id);
        Assert.DoesNotContain(published, t => t.Id == hidden.Id);
        await (await admin.PostAsJsonAsync("/api/cms/testimonials", new { quote = "", attribution = "x", order = 0, isPublished = true }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
    }

    [Theory]
    [InlineData(7, 7, "7")]
    [InlineData(47, 40, "40+")]
    [InlineData(50, 50, "50")]
    [InlineData(523, 500, "500+")]
    [InlineData(1260, 1200, "1200+")]
    public void Counts_are_rounded_down_to_tidy_figures(int n, int value, string label)
    {
        var tidy = typeof(PublicStats).Assembly.GetType("Maydel.Application.Website.Public.GetPublicStatsHandler")!
            .GetMethod("Tidy")!.Invoke(null, [n]);
        Assert.Equal((value, label), ((int, string))tidy!);
    }
}
