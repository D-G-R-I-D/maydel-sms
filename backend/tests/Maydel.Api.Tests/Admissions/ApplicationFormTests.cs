using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Academics;
using Maydel.Application.Admissions;
using Maydel.Application.Admissions.SaveApplicationForm;
using Maydel.Domain.Admissions;
using Maydel.Domain.Common;
using SkiaSharp;
using UglyToad.PdfPig;

namespace Maydel.Api.Tests.Admissions;

/// <summary>E11.5: fill the form after paying, attach documents, submit (then locked), PDFs blank and filled.</summary>
[Collection(nameof(ApiCollection))]
public sealed class ApplicationFormTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task A_family_fills_the_form_attaches_documents_and_submits()
    {
        var (session, family, application) = await PaidApplicationAsync();

        var draft = await ReadAsync<ApplicationDetail>(await family.PutAsJsonAsync($"/api/admissions/applications/{application}/form", Body(declaration: false)));
        Assert.True(draft.CanEdit);
        Assert.Contains("declaration", draft.MissingToSubmit);
        Assert.Contains("documents", draft.MissingToSubmit);

        await (await UploadAsync(family, application, "PassportPhoto", "%PDF-1.4\n"u8.ToArray())).AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        await ReadAsync<ApplicationDetail>(await UploadAsync(family, application, "PassportPhoto", Jpeg()));
        var withDocs = await ReadAsync<ApplicationDetail>(await UploadAsync(family, application, "BirthCertificate", "%PDF-1.4\n%cert\n"u8.ToArray()));
        Assert.Equal(2, withDocs.Documents.Count);
        var file = await family.GetAsync($"/api/admissions/applications/{application}/documents/{withDocs.Documents[0].Id}/file");
        Assert.Equal("attachment", file.Content.Headers.ContentDisposition?.DispositionType);

        await (await family.PostAsync($"/api/admissions/applications/{application}/submit", null)).AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        await ReadAsync<ApplicationDetail>(await family.PutAsJsonAsync($"/api/admissions/applications/{application}/form", Body(declaration: true)));
        var submitted = await ReadAsync<ApplicationDetail>(await family.PostAsync($"/api/admissions/applications/{application}/submit", null));

        Assert.Equal((ApplicationStatus.Submitted, false), (submitted.Application.Status, submitted.CanEdit));
        await (await family.PutAsJsonAsync($"/api/admissions/applications/{application}/form", Body(declaration: true))).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "admission.form_locked");

        var text = await PdfTextAsync(await family.GetAsync($"/api/admissions/applications/{application}/form.pdf"));
        Assert.Contains("Okonkwo", text);
        Assert.Contains(submitted.Application.ReferenceNumber, text);
        // The blank form is for the office only: online, the form unlocks after the fee is paid.
        var staff = await Api.ClientAsync(Roles.Admin);
        Assert.Contains("APPLICATION FOR ADMISSION", await PdfTextAsync(await staff.GetAsync($"/api/admissions/blank-form.pdf?sessionId={session}")));
        Assert.NotEqual(HttpStatusCode.OK, (await Api.CreateClient().GetAsync($"/api/public/admissions/blank-form.pdf?sessionId={session}")).StatusCode); // the old public address is gone
        Assert.Equal(HttpStatusCode.Unauthorized, (await Api.CreateClient().GetAsync($"/api/admissions/blank-form.pdf?sessionId={session}")).StatusCode);

        Assert.Equal(2, (await ReadAsync<ApplicationDetail>(await staff.GetAsync($"/api/admissions/applications/{application}/detail"))).Documents.Count);
    }

    [Fact]
    public async Task Only_an_admin_can_correct_a_submitted_form()
    {
        var (_, family, application) = await PaidApplicationAsync();
        var admin = await Api.ClientAsync(Roles.Admin);
        var corrected = new
        {
            childFirstName = "Chidiebere", childLastName = "Okonkwo", childDateOfBirth = "2019-04-05",
            form = new { childSex = "Male", homeAddress = "7 New Road, Abuja", fatherName = "Mr. Okonkwo", fatherPhone = "08030001111", declaration = true },
        };
        await (await admin.PutAsJsonAsync($"/api/admissions/applications/{application}/correction", corrected)).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "admission.not_submitted");

        await ReadAsync<ApplicationDetail>(await family.PutAsJsonAsync($"/api/admissions/applications/{application}/form", Body(declaration: true)));
        await ReadAsync<ApplicationDetail>(await UploadAsync(family, application, "PassportPhoto", Jpeg()));
        await ReadAsync<ApplicationDetail>(await family.PostAsync($"/api/admissions/applications/{application}/submit", null));

        var detail = await ReadAsync<ApplicationDetail>(await admin.PutAsJsonAsync($"/api/admissions/applications/{application}/correction", corrected));
        Assert.Equal(("7 New Road, Abuja", "Chidiebere", ApplicationStatus.Submitted), (detail.Form.HomeAddress, detail.Application.ChildFirstName, detail.Application.Status));

        var bursar = await Api.ClientAsync(Roles.Bursar);
        await (await bursar.PutAsJsonAsync($"/api/admissions/applications/{application}/correction", corrected)).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await family.PutAsJsonAsync($"/api/admissions/applications/{application}/correction", corrected)).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_form_is_locked_before_payment_and_private_to_the_family()
    {
        var (session, level) = await OpenAsync();
        var email = $"form-{Guid.NewGuid():N}@example.com";
        var unpaid = await ReadAsync<ApplicationDto>(await RegisterAsync(session, level, email));
        var family = Api.CreateClient((await Api.LoginAsync(email, "Apply#Pass2026")).AccessToken);

        await (await family.PutAsJsonAsync($"/api/admissions/applications/{unpaid.Id}/form", Body(true))).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "admission.form_locked");
        var (_, other, _) = await PaidApplicationAsync();
        await (await other.GetAsync($"/api/admissions/applications/{unpaid.Id}")).AssertProblemAsync(HttpStatusCode.NotFound);
        await (await other.GetAsync($"/api/admissions/applications/{unpaid.Id}/form.pdf")).AssertProblemAsync(HttpStatusCode.NotFound);
    }

    // ---------- helpers ----------

    private static int _year = 9000 + Random.Shared.Next(0, 900);

    private static object Body(bool declaration) => new
    {
        childFirstName = "Chidi", childLastName = "Okonkwo", childDateOfBirth = "2019-04-04",
        form = new { childSex = "Male", homeAddress = "12 Crescent, Gwarinpa, Abuja", fatherName = "Mr. Okonkwo", fatherPhone = "08030001111", declaration, medicalNotes = "Peanut allergy" },
    };

    private async Task<(Guid Session, Guid Level)> OpenAsync()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var year = Interlocked.Increment(ref _year);
        var session = (await (await admin.PostAsJsonAsync("/api/academics/sessions", new { name = $"{year}/{year + 1}", startsOn = $"{year}-09-14", endsOn = $"{year + 1}-07-23" }))
            .Content.ReadFromJsonAsync<SessionDto>(Json.Options))!;
        (await admin.PutAsJsonAsync("/api/admissions/settings", new { sessionId = session.Id, isOpen = true, formFee = 5000, examWindowDays = 5 })).EnsureSuccessStatusCode();
        var level = (await admin.GetFromJsonAsync<List<ClassLevelDto>>("/api/academics/class-levels", Json.Options))!.First().Id;
        return (session.Id, level);
    }

    private Task<HttpResponseMessage> RegisterAsync(Guid session, Guid level, string email) =>
        Api.CreateClient().PostAsJsonAsync("/api/public/admissions/register", new
        {
            parentName = "Mrs. Okonkwo", email, phone = "08031234567", password = "Apply#Pass2026", childFirstName = "Chidi", childLastName = "Okonkwo",
            classLevelId = level, sessionId = session,
        });

    private async Task<(Guid Session, HttpClient Family, Guid Application)> PaidApplicationAsync()
    {
        var (session, level) = await OpenAsync();
        var email = $"form-{Guid.NewGuid():N}@example.com";
        var application = await ReadAsync<ApplicationDto>(await RegisterAsync(session, level, email));
        var family = Api.CreateClient((await Api.LoginAsync(email, "Apply#Pass2026")).AccessToken);
        var code = (await (await (await Api.ClientAsync(Roles.Bursar)).PostAsJsonAsync("/api/admissions/form-codes", new { sessionId = session, count = 1, amountPaid = 5000 }))
            .Content.ReadFromJsonAsync<List<FormCodeDto>>(Json.Options))!.Single().Code;
        (await family.PostAsJsonAsync($"/api/admissions/applications/{application.Id}/redeem-code", new { code })).EnsureSuccessStatusCode();
        return (session, family, application.Id);
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid application, string kind, byte[] bytes)
    {
        var form = new MultipartFormDataContent { { new StringContent(kind), "kind" } };
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(content, "file", "upload");
        return client.PostAsync($"/api/admissions/applications/{application}/documents", form);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(Json.Options))!;
    }

    private static async Task<string> PdfTextAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        using var pdf = PdfDocument.Open(await response.Content.ReadAsByteArrayAsync());
        return string.Join("\n", pdf.GetPages().Select(p => p.Text));
    }

    private static byte[] Jpeg()
    {
        using var surface = SKSurface.Create(new SKImageInfo(300, 400));
        surface.Canvas.Clear(SKColors.SkyBlue);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
        return data.ToArray();
    }
}
