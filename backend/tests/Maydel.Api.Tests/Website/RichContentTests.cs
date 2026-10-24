using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Website;
using Maydel.Application.Website.Media;
using Maydel.Domain.Common;
using SkiaSharp;

namespace Maydel.Api.Tests.Website;

/// <summary>Formatted posts and events from the editor: cleaned to an allow-list, plain text derived, pictures from our own store.</summary>
[Collection(nameof(ApiCollection))]
public sealed class RichContentTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public void Only_safe_formatting_survives_and_the_plain_text_reads_well()
    {
        var (html, text) = RichText.Clean("""
            <h2 onclick="x()">Sports day</h2><p style="color:red">Join us <strong>Friday</strong> at <a href="https://maydel.sch.ng/events" target="_blank">the field</a>.</p>
            <script>alert(1)</script><iframe src="https://evil.test"></iframe>
            <ul><li>Races</li><li>Prizes</li></ul>
            <img src="https://tracker.test/pixel.gif"><img src="/api/public/site/images/media/0198?v=1" alt="Field" class="wide evil">
            <a href="javascript:alert(1)">bad</a>
            """)!.Value;

        Assert.DoesNotContain("onclick", html);
        Assert.DoesNotContain("style=", html);
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("<iframe", html);
        Assert.DoesNotContain("tracker.test", html);
        Assert.DoesNotContain("javascript:", html);
        Assert.DoesNotContain("evil", html);
        Assert.Contains("<h2>Sports day</h2>", html);
        Assert.Contains("rel=\"noopener noreferrer\"", html);
        Assert.Contains("src=\"/api/public/site/images/media/0198?v=1\"", html);
        Assert.Contains("class=\"wide\"", html);
        Assert.StartsWith("Sports day\n\nJoin us Friday at the field.", text);
        Assert.Contains("• Races\n• Prizes", text);
        Assert.Null(RichText.Clean("<p> </p><script>x</script>"));
    }

    [Fact]
    public async Task A_post_written_in_the_editor_keeps_clean_html_and_its_pictures_are_public()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Png());
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "field.png");
        var upload = await admin.PostAsync("/api/cms/media", form);
        upload.EnsureSuccessStatusCode();
        var media = (await upload.Content.ReadFromJsonAsync<MediaDto>(Json.Options))!;
        Assert.StartsWith(RichText.ImagePrefix + "media/", media.Url);
        Assert.Equal(HttpStatusCode.OK, (await Api.CreateClient().GetAsync(media.Url)).StatusCode);

        var saved = await admin.PostAsJsonAsync("/api/cms/posts", new
        {
            title = $"Rich {Guid.NewGuid():N}", body = "", audience = "Everyone",
            bodyHtml = $"<p>Hello <em>parents</em><script>x</script></p><figure><img src=\"{media.Url}\" alt=\"Field\"><figcaption>The field</figcaption></figure>",
        });
        saved.EnsureSuccessStatusCode();
        var post = (await saved.Content.ReadFromJsonAsync<PostDto>(Json.Options))!;
        Assert.Contains("<em>parents</em>", post.BodyHtml);
        Assert.DoesNotContain("<script", post.BodyHtml);
        Assert.Contains(media.Url, post.BodyHtml);
        Assert.Equal("Hello parents\n\nThe field", post.Body);

        // Back to plain text: the HTML goes.
        var plain = await admin.PutAsJsonAsync($"/api/cms/posts/{post.Id}", new { title = post.Title, body = "Just text", audience = "Everyone" });
        Assert.Null((await plain.Content.ReadFromJsonAsync<PostDto>(Json.Options))!.BodyHtml);

        await (await (await Api.ClientAsync(Roles.Teacher)).PostAsync("/api/cms/media", new MultipartFormDataContent())).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    private static byte[] Png()
    {
        using var surface = SKSurface.Create(new SKImageInfo(320, 200));
        surface.Canvas.Clear(SKColors.SeaGreen);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }
}
