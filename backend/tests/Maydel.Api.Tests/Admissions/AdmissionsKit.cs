using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Academics;
using Maydel.Application.Admissions;
using Maydel.Application.Admissions.NotifyStageOnePasses;
using Maydel.Application.Admissions.SweepAdmissions;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Admissions;
using Maydel.Domain.Common;
using Maydel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Maydel.Api.Tests.Admissions;

/// <summary>Shared steps for the admissions API tests: open a session, buy and submit forms, exam sittings, the sweep, outbox checks.</summary>
internal static class AdmissionsKit
{

    private static int _year = 2850 + Random.Shared.Next(0, 100);

    // The fake clock: 08:00 in Abuja on Thursday 15 October 2026; a 5-day window ends on the 20th.
    public static string Wat(int day, int hour) => $"2026-10-{day:00}T{hour:00}:00:00+01:00";

    public static async Task<Guid> OpenSessionAsync(ApiFactory Api, int? year = null)
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var y = year ?? Interlocked.Increment(ref _year);
        var created = await admin.PostAsJsonAsync("/api/academics/sessions", new { name = $"{y}/{y + 1}", startsOn = $"{y}-09-13", endsOn = $"{y + 1}-07-23" });
        // A fixed year (e.g. 2027, near the fake clock) may already exist from another test: reuse it.
        var session = created.IsSuccessStatusCode
            ? (await created.Content.ReadFromJsonAsync<SessionDto>(Json.Options))!
            : (await admin.GetFromJsonAsync<List<SessionDto>>("/api/academics/sessions", Json.Options))!.Single(x => x.Name == $"{y}/{y + 1}");
        (await admin.PutAsJsonAsync("/api/admissions/settings", new { sessionId = session.Id, isOpen = true, formFee = 5000, examWindowDays = 5 })).EnsureSuccessStatusCode();
        return session.Id;
    }

    public static object SlotBody(Guid session, string startsAt, int capacity, string venue, string mode = "Paper") =>
        new { sessionId = session, startsAt, durationMinutes = 90, venue, mode, capacity };

    public static async Task<ExamSlotDto> SlotAsync(HttpClient admin, Guid session, string startsAt, int capacity, string venue, string mode = "Paper") =>
        await ReadAsync<ExamSlotDto>(await admin.PostAsJsonAsync("/api/admissions/exam-slots", SlotBody(session, startsAt, capacity, venue, mode)));

    public static async Task<(HttpClient Family, Guid Application)> PaidAsync(ApiFactory Api, Guid session)
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var level = (await admin.GetFromJsonAsync<List<ClassLevelDto>>("/api/academics/class-levels", Json.Options))!.First().Id;
        var email = $"exam-{Guid.NewGuid():N}@example.com";
        var application = await ReadAsync<ApplicationDto>(await Api.CreateClient().PostAsJsonAsync("/api/public/admissions/register", new
        {
            parentName = "Mrs. Bello", email, phone = "08031234567", password = "Apply#Pass2026", childFirstName = "Amina", childLastName = "Bello",
            classLevelId = level, sessionId = session,
        }));
        var family = Api.CreateClient((await Api.LoginAsync(email, "Apply#Pass2026")).AccessToken);
        var code = (await (await (await Api.ClientAsync(Roles.Bursar)).PostAsJsonAsync("/api/admissions/form-codes", new { sessionId = session, count = 1, amountPaid = 5000 }))
            .Content.ReadFromJsonAsync<List<FormCodeDto>>(Json.Options))!.Single().Code;
        (await family.PostAsJsonAsync($"/api/admissions/applications/{application.Id}/redeem-code", new { code })).EnsureSuccessStatusCode();
        return (family, application.Id);
    }

    public static async Task<(HttpClient Family, Guid Application)> SubmittedAsync(ApiFactory Api, Guid session)
    {
        var (family, id) = await PaidAsync(Api, session);
        (await family.PutAsJsonAsync($"/api/admissions/applications/{id}/form", new
        {
            childFirstName = "Amina", childLastName = "Bello", childDateOfBirth = "2019-06-06",
            form = new { childSex = "Female", homeAddress = "3 Close, Wuse, Abuja", motherName = "Mrs. Bello", motherPhone = "08030002222", declaration = true },
        })).EnsureSuccessStatusCode();
        var upload = new MultipartFormDataContent { { new StringContent("PassportPhoto"), "kind" } };
        var photo = new ByteArrayContent(Jpeg());
        photo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        upload.Add(photo, "file", "photo");
        (await family.PostAsync($"/api/admissions/applications/{id}/documents", upload)).EnsureSuccessStatusCode();
        (await family.PostAsync($"/api/admissions/applications/{id}/submit", null)).EnsureSuccessStatusCode();
        return (family, id);
    }

    public static async Task<SweepResult> SweepAsync(ApiFactory Api, DateTimeOffset now, Guid session)
    {
        SweepResult? result = null;
        await Api.WithServicesAsync(async sp =>
            result = (await sp.GetRequiredService<ICommandHandler<SweepAdmissionsCommand, SweepResult>>().Handle(new SweepAdmissionsCommand(now, session), CancellationToken.None)).Value);
        return result!;
    }

    public static async Task<int> OutboxCountAsync(ApiFactory Api, Guid application, string subjectLike)
    {
        var count = 0;
        await Api.WithServicesAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var reference = await db.Applications.Where(a => a.Id == application).Select(a => a.ReferenceNumber).SingleAsync();
            count = await db.OutboxEmails.CountAsync(e => e.Subject.Contains(reference) && EF.Functions.ILike(e.Subject, subjectLike));
        });
        return count;
    }

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode) Assert.Fail($"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json.Options))!;
    }

    public static byte[] Jpeg()
    {
        using var surface = SKSurface.Create(new SKImageInfo(300, 400));
        surface.Canvas.Clear(SKColors.SkyBlue);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
        return data.ToArray();
    }
}
