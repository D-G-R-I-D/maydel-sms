using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Assessment;
using Maydel.Application.SchoolSettings;
using Maydel.Domain.Common;
using SkiaSharp;
using UglyToad.PdfPig;

namespace Maydel.Api.Tests.SchoolSettings;

/// <summary>E2.6 school profile: admins edit it, report cards print it, the logo is public and the signature admin-only.</summary>
[Collection(nameof(ApiCollection))]
public sealed class SchoolProfileTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task Admin_edits_the_profile_and_report_cards_print_it()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var original = await ProfileAsync(admin);
        var tag = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        try
        {
            var response = await admin.PutAsJsonAsync("/api/school/profile", new
            {
                name = $"Maydel Spring Schools {tag}", motto = "Knowledge and character", address = " 1 School Road, Abuja ",
                phone = "", email = "office@maydel.test", website = (string?)null, principalName = $"Mrs. Principal {tag}",
            });
            response.EnsureSuccessStatusCode();
            var saved = (await response.Content.ReadFromJsonAsync<SchoolProfileDto>(Json.Options))!;
            Assert.Equal(("1 School Road, Abuja", (string?)null, (string?)null), (saved.Address, saved.Phone, saved.Website));
            Assert.Equal(saved, await ProfileAsync(await Api.ClientAsync(Roles.Teacher)));

            var text = await ReportPdfTextAsync(admin);
            Assert.Contains($"MAYDEL SPRING SCHOOLS {tag}", text);
            Assert.Contains($"Mrs. Principal {tag}", text);
            Assert.Contains("1 School Road, Abuja", text);
        }
        finally
        {
            await RestoreAsync(admin, original);
        }
    }

    [Fact]
    public async Task Logo_is_public_the_signature_is_admin_only_and_both_print_on_the_report()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        try
        {
            var logo = await UploadAsync(admin, "/api/school/profile/logo", Png(1200, 400));
            logo.EnsureSuccessStatusCode();
            var profile = (await logo.Content.ReadFromJsonAsync<SchoolProfileDto>(Json.Options))!;
            Assert.NotNull(profile.LogoVersion);

            var publicLogo = await Api.CreateClient().GetAsync($"/api/public/school/logo?v={profile.LogoVersion}");
            Assert.Equal("image/png", publicLogo.Content.Headers.ContentType?.MediaType);
            using (var decoded = SKBitmap.Decode(await publicLogo.Content.ReadAsByteArrayAsync()))
            {
                Assert.Equal((600, 200), (decoded.Width, decoded.Height));
                Assert.Equal(0, decoded.GetPixel(0, 0).Alpha); // transparency kept
            }

            (await UploadAsync(admin, "/api/school/profile/signature", Png(500, 150))).EnsureSuccessStatusCode();
            Assert.Equal("image/png", (await admin.GetAsync("/api/school/profile/signature")).Content.Headers.ContentType?.MediaType);
            await (await (await Api.ClientAsync(Roles.Teacher)).GetAsync("/api/school/profile/signature")).AssertProblemAsync(HttpStatusCode.Forbidden);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Api.CreateClient().GetAsync("/api/school/profile/signature")).StatusCode);

            var (pages, images) = await ReportPdfImagesAsync(admin);
            Assert.Equal(1, pages);
            Assert.True(images >= 2, $"expected the logo and the signature on the page, found {images} images");
        }
        finally
        {
            (await admin.DeleteAsync("/api/school/profile/logo")).EnsureSuccessStatusCode();
            var cleared = (await (await admin.DeleteAsync("/api/school/profile/signature")).Content.ReadFromJsonAsync<SchoolProfileDto>(Json.Options))!;
            Assert.Equal(((string?)null, (string?)null), (cleared.LogoVersion, cleared.SignatureVersion));
        }
        await (await Api.CreateClient().GetAsync("/api/public/school/logo")).AssertProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Name_and_address_are_required_and_email_must_be_valid()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var response = await admin.PutAsJsonAsync("/api/school/profile", new { name = " ", address = "", email = "not-an-email" });
        var errors = (await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation")).GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("address", out _));
        Assert.True(errors.TryGetProperty("email", out _));
    }

    [Fact]
    public async Task Only_images_are_accepted()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        await (await UploadAsync(admin, "/api/school/profile/logo", "<svg onload=alert(1)>"u8.ToArray())).AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
    }

    [Theory]
    [InlineData(Roles.Teacher)]
    [InlineData(Roles.Bursar)]
    public async Task Other_staff_can_read_but_not_change_the_profile(string role)
    {
        var client = await Api.ClientAsync(role);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/school/profile")).StatusCode);
        await (await client.PutAsJsonAsync("/api/school/profile", new { name = "X", address = "Y" })).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await UploadAsync(client, "/api/school/profile/logo", Png(10, 10))).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(Roles.Parent)]
    [InlineData(Roles.Student)]
    public async Task Families_cannot_read_the_staff_profile(string role) =>
        await (await (await Api.ClientAsync(role)).GetAsync("/api/school/profile")).AssertProblemAsync(HttpStatusCode.Forbidden);

    // ---------- helpers ----------

    private static async Task<SchoolProfileDto> ProfileAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<SchoolProfileDto>("/api/school/profile", Json.Options))!;

    private static async Task RestoreAsync(HttpClient admin, SchoolProfileDto p) =>
        (await admin.PutAsJsonAsync("/api/school/profile", new { p.Name, p.Motto, p.Address, p.Phone, p.Email, p.Website, p.PrincipalName })).EnsureSuccessStatusCode();

    private async Task<byte[]> ReportPdfAsync(HttpClient admin)
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var settings = (await admin.GetFromJsonAsync<AssessmentSettings>("/api/assessment/settings", Json.Options))!;
        var cat = settings.Components.Single(c => c.Name == "CAT" && c.IsActive).Id;
        (await admin.PutAsJsonAsync("/api/assessment/gradesheet", new
        {
            classRoomId = s.ClassRoomId, subjectId = s.SubjectId, termId = s.TermId,
            entries = new[] { new { studentId = s.Students[0], scores = new[] { new { componentId = cat, score = (decimal?)25 } } } },
        })).EnsureSuccessStatusCode();
        var response = await admin.GetAsync($"/api/reports/{s.Students[0]}/{s.TermId}/pdf");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }

    private async Task<string> ReportPdfTextAsync(HttpClient admin)
    {
        using var pdf = PdfDocument.Open(await ReportPdfAsync(admin));
        return pdf.GetPage(1).Text;
    }

    private async Task<(int Pages, int Images)> ReportPdfImagesAsync(HttpClient admin)
    {
        using var pdf = PdfDocument.Open(await ReportPdfAsync(admin));
        return (pdf.NumberOfPages, pdf.GetPage(1).GetImages().Count());
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string url, byte[] bytes)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "image.png");
        return await client.PutAsync(url, form);
    }

    /// <summary>A PNG with a transparent background and a solid shape in the middle, like a logo or signature.</summary>
    private static byte[] Png(int width, int height)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint { Color = SKColors.Navy };
        surface.Canvas.DrawRect(width / 4f, height / 4f, width / 2f, height / 2f, paint);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
