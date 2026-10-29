using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Maydel.Api.Tests.Admissions;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Common;
using Maydel.Application.Website;
using Maydel.Domain.Admissions;
using Maydel.Domain.Common;

namespace Maydel.Api.Tests.Website;

/// <summary>E7.3 / E7.9 / E7.10: news and notices, carousel and ticker, events, gallery, staff profiles, contact form.</summary>
[Collection(nameof(ApiCollection))]
public sealed class WebsiteTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task Admins_publish_posts_that_reach_the_news_notices_carousel_and_ticker()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var tag = Guid.NewGuid().ToString("N")[..6];
        var post = await AdmissionsKit.ReadAsync<PostDto>(await admin.PostAsJsonAsync("/api/cms/posts", Post($"Admission forms on sale {tag}", featured: true)));
        Assert.Equal($"admission-forms-on-sale-{tag}", post.Slug);
        Assert.Null(post.PublishedAt);

        // Drafts never show.
        var site = Api.CreateClient();
        Assert.DoesNotContain(await AdmissionsKit.ReadAsync<List<PublicNotice>>(await site.GetAsync("/api/public/site/notices")), n => n.Id == post.Id);
        await (await site.GetAsync($"/api/public/site/news/{post.Slug}")).AssertProblemAsync(HttpStatusCode.NotFound);

        var form = new MultipartFormDataContent();
        var image = new ByteArrayContent(AdmissionsKit.Jpeg());
        image.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(image, "file", "banner.jpg");
        var withImage = await AdmissionsKit.ReadAsync<PostDto>(await admin.PutAsync($"/api/cms/posts/{post.Id}/image", form));
        Assert.NotNull(withImage.ImageUrl);
        var published = await AdmissionsKit.ReadAsync<PostDto>(await admin.PostAsync($"/api/cms/posts/{post.Id}/publish", null));
        Assert.NotNull(published.PublishedAt);

        var notice = Assert.Single(await AdmissionsKit.ReadAsync<List<PublicNotice>>(await site.GetAsync("/api/public/site/notices")), n => n.Id == post.Id);
        Assert.Equal((true, "/admissions/apply", "Buy form"), (notice.IsFeatured, notice.LinkUrl, notice.LinkText));
        var highlights = await AdmissionsKit.ReadAsync<Highlights>(await site.GetAsync("/api/public/site/highlights"));
        Assert.Contains(highlights.Slides, s => s.Id == post.Id && s.Href == "/admissions/apply");
        Assert.Contains(highlights.Ticker, t => t.Id == post.Id);
        var article = await AdmissionsKit.ReadAsync<NewsArticle>(await site.GetAsync($"/api/public/site/news/{post.Slug}"));
        Assert.Equal(post.Id, article.Id);
        Assert.Contains((await AdmissionsKit.ReadAsync<PagedResult<NewsSummary>>(await site.GetAsync("/api/public/site/news?pageSize=50"))).Items, n => n.Id == post.Id);

        // The picture is public once published, with a long public cache.
        var picture = await site.GetAsync(article.ImageUrl!);
        picture.EnsureSuccessStatusCode();
        Assert.Contains("public", picture.Headers.CacheControl?.ToString());

        // Editing keeps the web address; the pop-up version changes so visitors see it again.
        var edited = await AdmissionsKit.ReadAsync<PostDto>(await admin.PutAsJsonAsync($"/api/cms/posts/{post.Id}", Post($"Forms on sale now {tag}", featured: true)));
        Assert.Equal(post.Slug, edited.Slug);
        Assert.NotEqual(notice.Version, Assert.Single(await AdmissionsKit.ReadAsync<List<PublicNotice>>(await site.GetAsync("/api/public/site/notices")), n => n.Id == post.Id).Version);

        // A second post with the same title gets its own address; a parents-only post can't go on the website.
        var twin = await AdmissionsKit.ReadAsync<PostDto>(await admin.PostAsJsonAsync("/api/cms/posts", Post($"Admission forms on sale {tag}", featured: false)));
        Assert.Equal($"admission-forms-on-sale-{tag}-2", twin.Slug);
        await (await admin.PostAsJsonAsync("/api/cms/posts", new { title = "x", body = "y", audience = "Parents", inTicker = true }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation");

        (await admin.PostAsync($"/api/cms/posts/{post.Id}/unpublish", null)).EnsureSuccessStatusCode();
        await (await site.GetAsync(article.ImageUrl!)).AssertProblemAsync(HttpStatusCode.NotFound);
        await (await (await Api.ClientAsync(Roles.Teacher)).GetAsync("/api/cms/posts")).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Events_gallery_staff_and_the_contact_form()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var site = Api.CreateClient();
        var tag = Guid.NewGuid().ToString("N")[..6];

        var open = await AdmissionsKit.ReadAsync<EventDto>(await admin.PostAsJsonAsync("/api/cms/events", new
        {
            title = $"Open day {tag}", startsAt = "2026-11-07T10:00:00+01:00", endsAt = "2026-11-07T14:00:00+01:00", location = "Main hall", audience = "Everyone",
        }));
        var staffOnly = await AdmissionsKit.ReadAsync<EventDto>(await admin.PostAsJsonAsync("/api/cms/events", new
        {
            title = $"INSET day {tag}", startsAt = "2026-11-09T08:00:00+01:00", audience = "Staff",
        }));
        var events = await AdmissionsKit.ReadAsync<List<PublicEvent>>(await site.GetAsync("/api/public/site/events?take=100"));
        Assert.Contains(events, e => e.Id == open.Id);
        Assert.DoesNotContain(events, e => e.Id == staffOnly.Id);
        await (await admin.PostAsJsonAsync("/api/cms/events", new { title = "Bad", startsAt = "2026-11-07T10:00:00+01:00", endsAt = "2026-11-07T09:00:00+01:00", audience = "Everyone" }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation");

        var form = new MultipartFormDataContent
        {
            { new StringContent($"Relay race {tag}"), "title" }, { new StringContent($"Sports {tag}"), "category" }, { new StringContent("true"), "isFeatured" },
        };
        var image = new ByteArrayContent(AdmissionsKit.Jpeg());
        image.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(image, "file", "relay.jpg");
        var photo = await AdmissionsKit.ReadAsync<GalleryPhotoDto>(await admin.PostAsync("/api/cms/gallery", form));
        var gallery = await AdmissionsKit.ReadAsync<PublicGallery>(await site.GetAsync($"/api/public/site/gallery?category={Uri.EscapeDataString($"Sports {tag}")}"));
        Assert.Equal(photo.Id, Assert.Single(gallery.Photos).Id);
        Assert.Contains($"Sports {tag}", gallery.Categories);
        Assert.Contains((await AdmissionsKit.ReadAsync<Highlights>(await site.GetAsync("/api/public/site/highlights"))).Slides, s => s.Id == photo.Id);
        (await site.GetAsync(photo.ImageUrl)).EnsureSuccessStatusCode();

        // Staff appear only when chosen; others' photos stay private.
        var (_, teacher) = await SchoolBuilder.TeacherAsync(Api);
        await (await site.GetAsync($"/api/public/site/images/staff/{teacher.Id}")).AssertProblemAsync(HttpStatusCode.NotFound);
        var shown = await AdmissionsKit.ReadAsync<PublicStaff>(await admin.PutAsJsonAsync($"/api/cms/staff/{teacher.Id}",
            new { showOnWebsite = true, jobTitle = "Head of Science", bio = "Twelve years teaching.", websiteOrder = 1, displayName = "Mr. T. Science" }));
        Assert.Equal(("Head of Science", "Mr. T. Science"), (shown.JobTitle, shown.Name));
        Assert.Contains(await AdmissionsKit.ReadAsync<List<PublicStaff>>(await site.GetAsync("/api/public/site/staff")), x => x.Id == teacher.Id);
        // The console lists every active staff member with what's set, to prefill the form and order the page.
        var row = Assert.Single(await AdmissionsKit.ReadAsync<List<Maydel.Application.Website.StaffProfiles.WebsiteStaffRow>>(await admin.GetAsync("/api/cms/staff")),
            x => x.StaffId == teacher.Id);
        Assert.Equal((true, "Head of Science", "Twelve years teaching.", 1, false, "Mr. T. Science"),
            (row.ShowOnWebsite, row.JobTitle, row.Bio, row.WebsiteOrder, row.HasPhoto, row.DisplayName));
        Assert.NotEqual("Mr. T. Science", row.Name); // the staff record keeps the full name

        // Contact form: stored for the office; the honeypot quietly drops bots.
        var subject = $"Fees enquiry {tag}";
        Assert.Equal(HttpStatusCode.NoContent, (await site.PostAsJsonAsync("/api/public/site/contact",
            new { name = "Mrs. Eze", email = "eze@example.com", phone = "08031112222", subject, message = "What are the fees for Year 3?" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await site.PostAsJsonAsync("/api/public/site/contact",
            new { name = "Bot", email = "bot@example.com", subject = $"spam {tag}", message = "Buy now!!!", website = "http://spam" })).StatusCode);
        await (await site.PostAsJsonAsync("/api/public/site/contact", new { name = "x", email = "not-an-email", message = "hello there" }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        var inbox = await AdmissionsKit.ReadAsync<PagedResult<ContactMessageDto>>(await admin.GetAsync("/api/cms/messages?status=New&pageSize=100"));
        var received = Assert.Single(inbox.Items, m => m.Subject == subject);
        Assert.DoesNotContain(inbox.Items, m => m.Subject == $"spam {tag}");
        Assert.Equal(MessageStatus.Replied,
            (await AdmissionsKit.ReadAsync<ContactMessageDto>(await admin.PutAsJsonAsync($"/api/cms/messages/{received.Id}/status", new { status = "Replied" }))).Status);
    }

    private static object Post(string title, bool featured) => new
    {
        title, summary = "Forms for the 2027/2028 session", body = "Buy online or at the school office.", category = "Admissions", audience = "Everyone",
        isFeatured = featured, linkUrl = "/admissions/apply", linkText = "Buy form", inTicker = true, inCarousel = true,
    };
}

[Collection(nameof(ApiCollection))]
public sealed class SchoolContactTests(PostgresFixture fixture)
{
    [Fact]
    public async Task The_website_reads_the_school_contact_details_from_settings()
    {
        var api = fixture.Api;
        var admin = await api.ClientAsync(Roles.Admin);
        var current = await AdmissionsKit.ReadAsync<Maydel.Application.SchoolSettings.SchoolProfileDto>(await admin.GetAsync("/api/school/profile"));
        var saved = await AdmissionsKit.ReadAsync<Maydel.Application.SchoolSettings.SchoolProfileDto>(await admin.PutAsJsonAsync("/api/school/profile", new
        {
            current.Name, current.Motto, current.Address, current.Phone, current.Email, current.Website, current.PrincipalName,
            whatsApp = "0803 123 4567", mapUrl = "https://maps.app.goo.gl/example", officeHours = "Mon–Fri 7:30am – 4:00pm",
        }));
        Assert.Equal("0803 123 4567", saved.WhatsApp);
        var site = await AdmissionsKit.ReadAsync<Maydel.Application.SchoolSettings.PublicSchoolProfile>(await api.CreateClient().GetAsync("/api/public/school/profile"));
        Assert.Equal(("https://wa.me/2348031234567", "https://maps.app.goo.gl/example"), (site.WhatsAppUrl, site.MapUrl));
        await (await admin.PutAsJsonAsync("/api/school/profile", new { current.Name, current.Address, mapUrl = "javascript:alert(1)" }))
            .AssertProblemAsync(System.Net.HttpStatusCode.BadRequest, "validation");
    }
}
