using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Academics;
using Maydel.Application.Photos;
using Maydel.Application.StaffMembers;
using Maydel.Application.Students;
using Maydel.Domain.Common;
using SkiaSharp;

namespace Maydel.Api.Tests.Photos;

/// <summary>E3.1 / E4.1 photos: strict image validation, re-encoding (metadata stripped), private access.</summary>
[Collection(nameof(ApiCollection))]
public sealed class PhotoTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;
    private const string GpsMarker = "GPS-LAT-9.0765-LNG-7.3986";

    [Fact]
    public async Task Upload_is_re_encoded_resized_and_stripped_of_metadata()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var student = await CreateStudentAsync(admin);

        var upload = await UploadAsync(admin, $"/api/students/{student.Id}/photo", JpegWithMetadata(1600, 1200), "photo.jpg", "image/jpeg");

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var info = (await upload.Content.ReadFromJsonAsync<PhotoInfo>(Json.Options))!;
        Assert.Equal((800, 600), (info.Width, info.Height));

        var photo = await admin.GetAsync($"/api/students/{student.Id}/photo");
        Assert.Equal("image/jpeg", photo.Content.Headers.ContentType?.MediaType);
        var bytes = await photo.Content.ReadAsByteArrayAsync();
        Assert.DoesNotContain(GpsMarker, Encoding.Latin1.GetString(bytes));
        Assert.DoesNotContain("Exif", Encoding.Latin1.GetString(bytes));
        using var decoded = SKBitmap.Decode(bytes);
        Assert.Equal(800, decoded.Width);

        var detail = (await admin.GetFromJsonAsync<StudentDetail>($"/api/students/{student.Id}", Json.Options))!;
        Assert.Equal(info.Version, detail.PhotoVersion);
    }

    [Fact]
    public async Task Photo_responses_are_privately_cacheable_and_support_conditional_requests()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var student = await CreateStudentAsync(admin);
        (await UploadAsync(admin, $"/api/students/{student.Id}/photo", Png(300, 300), "p.png", "image/png")).EnsureSuccessStatusCode();

        var first = await admin.GetAsync($"/api/students/{student.Id}/photo");
        Assert.True(first.Headers.CacheControl!.Private);
        Assert.Equal(TimeSpan.FromDays(1), first.Headers.CacheControl.MaxAge);
        Assert.NotNull(first.Headers.ETag);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/students/{student.Id}/photo");
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        Assert.Equal(HttpStatusCode.NotModified, (await admin.SendAsync(request)).StatusCode);
    }

    [Theory]
    [InlineData("photo.jpg", "image/jpeg")]   // text pretending to be a JPEG
    [InlineData("photo.png", "image/png")]
    [InlineData("photo.svg", "image/svg+xml")]
    public async Task Anything_that_is_not_a_real_image_is_rejected(string fileName, string contentType)
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var student = await CreateStudentAsync(admin);
        var fake = Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>");

        var response = await UploadAsync(admin, $"/api/students/{student.Id}/photo", fake, fileName, contentType);

        var errors = (await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation")).GetProperty("errors");
        Assert.True(errors.TryGetProperty("file", out _));
    }

    [Fact]
    public async Task Truncated_image_with_a_valid_header_is_rejected()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var student = await CreateStudentAsync(admin);
        var truncated = JpegWithMetadata(400, 300)[..40];

        await (await UploadAsync(admin, $"/api/students/{student.Id}/photo", truncated, "p.jpg", "image/jpeg"))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
    }

    [Fact]
    public async Task Replacing_changes_the_version_and_removing_makes_it_404()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var student = await CreateStudentAsync(admin);
        var url = $"/api/students/{student.Id}/photo";

        var v1 = (await (await UploadAsync(admin, url, Png(200, 200), "a.png", "image/png")).Content.ReadFromJsonAsync<PhotoInfo>(Json.Options))!.Version;
        var v2 = (await (await UploadAsync(admin, url, Png(250, 250), "b.png", "image/png")).Content.ReadFromJsonAsync<PhotoInfo>(Json.Options))!.Version;
        Assert.NotEqual(v1, v2);
        Assert.False(File.Exists(Path.Combine(ApiFactory.StorageRoot, "students", v1 + ".jpg"))); // old file cleaned up
        Assert.True(File.Exists(Path.Combine(ApiFactory.StorageRoot, "students", v2 + ".jpg")));

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync(url)).StatusCode);
        await (await admin.GetAsync(url)).AssertProblemAsync(HttpStatusCode.NotFound, "photo.not_found");
        Assert.Null((await admin.GetFromJsonAsync<StudentDetail>($"/api/students/{student.Id}", Json.Options))!.PhotoVersion);
    }

    [Fact]
    public async Task Staff_photos_upload_and_show_on_the_staff_record()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var created = await admin.PostAsJsonAsync("/api/staff", new { fullName = "Photo Teacher", email = ApiFactory.UniqueEmail(), roles = new[] { "Teacher" } });
        var staff = (await created.Content.ReadFromJsonAsync<StaffCreated>(Json.Options))!.Staff;

        var info = (await (await UploadAsync(admin, $"/api/staff/{staff.Id}/photo", Png(500, 500), "s.png", "image/png")).Content.ReadFromJsonAsync<PhotoInfo>(Json.Options))!;

        Assert.Equal(info.Version, (await admin.GetFromJsonAsync<StaffDetail>($"/api/staff/{staff.Id}", Json.Options))!.PhotoVersion);
        var teacher = await Api.ClientAsync(Roles.Teacher);
        Assert.Equal(HttpStatusCode.OK, (await teacher.GetAsync($"/api/staff/{staff.Id}/photo")).StatusCode);
    }

    [Theory]
    [InlineData(Roles.Teacher)]
    [InlineData(Roles.Bursar)]
    public async Task Staff_can_view_but_only_admins_change_photos(string role)
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var student = await CreateStudentAsync(admin);
        (await UploadAsync(admin, $"/api/students/{student.Id}/photo", Png(100, 100), "p.png", "image/png")).EnsureSuccessStatusCode();
        var client = await Api.ClientAsync(role);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/students/{student.Id}/photo")).StatusCode);
        await (await UploadAsync(client, $"/api/students/{student.Id}/photo", Png(100, 100), "p.png", "image/png")).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await client.DeleteAsync($"/api/students/{student.Id}/photo")).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(Roles.Parent)]
    [InlineData(Roles.Student)]
    public async Task Parents_and_students_cannot_fetch_photos_through_the_staff_api(string role)
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var student = await CreateStudentAsync(admin);
        (await UploadAsync(admin, $"/api/students/{student.Id}/photo", Png(100, 100), "p.png", "image/png")).EnsureSuccessStatusCode();
        var client = await Api.ClientAsync(role);

        await (await client.GetAsync($"/api/students/{student.Id}/photo")).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_requests_get_401()
    {
        await (await Api.CreateClient().GetAsync($"/api/students/{Guid.NewGuid()}/photo")).AssertProblemAsync(HttpStatusCode.Unauthorized);
    }

    // ---------- helpers ----------

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string url, byte[] bytes, string fileName, string contentType)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return await client.PutAsync(url, form);
    }

    private static byte[] Png(int width, int height)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        surface.Canvas.Clear(SKColors.SteelBlue);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>A real JPEG with an extra APP1 "Exif" segment carrying a fake GPS marker, like a phone photo.</summary>
    private static byte[] JpegWithMetadata(int width, int height)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        surface.Canvas.Clear(SKColors.Goldenrod);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        var jpeg = data.ToArray();

        var payload = new List<byte>();
        payload.AddRange("Exif\0\0"u8.ToArray());
        payload.AddRange(new byte[] { 0x49, 0x49, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00 }); // little-endian TIFF, empty IFD
        payload.AddRange(Encoding.ASCII.GetBytes(GpsMarker));
        var length = payload.Count + 2;
        var segment = new List<byte> { 0xFF, 0xE1, (byte)(length >> 8), (byte)(length & 0xFF) };
        segment.AddRange(payload);
        return [.. jpeg[..2], .. segment, .. jpeg[2..]];
    }

    private static async Task<StudentDetail> CreateStudentAsync(HttpClient admin)
    {
        var level = (await admin.GetFromJsonAsync<List<ClassLevelDto>>("/api/academics/class-levels", Json.Options))!.First();
        var room = await admin.PostAsJsonAsync("/api/academics/classrooms", new { classLevelId = level.Id, arm = "P" + Guid.NewGuid().ToString("N")[..8], capacity = 30 });
        var roomId = (await room.Content.ReadFromJsonAsync<ClassRoomDto>(Json.Options))!.Id;
        var response = await admin.PostAsJsonAsync("/api/students",
            new { firstName = "Photo", lastName = "Pupil", dateOfBirth = "2018-03-10", classRoomId = roomId, admittedOn = "2026-09-14" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<StudentDetail>(Json.Options))!;
    }
}
