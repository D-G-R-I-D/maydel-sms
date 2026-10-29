using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Communication;
using Maydel.Application.Identity;
using Maydel.Application.Notifications;
using Maydel.Application.Students;
using Maydel.Domain.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Communication;
using Microsoft.Extensions.DependencyInjection;

namespace Maydel.Api.Tests.Communication;

/// <summary>E17.2: one notebook per student between the form teacher (and admins) and the family; nobody else can open it.</summary>
[Collection(nameof(ApiCollection))]
public sealed class CommunicationBookTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;
    private static int _phone = Random.Shared.Next(1_000_000, 8_000_000);

    [Fact]
    public async Task The_form_teacher_and_the_family_write_to_each_other_and_see_what_is_unread()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var parent = await ParentAsync(s.Students[0]);
        var child = s.Students[0];

        var note = await EntryAsync(await s.FormTeacher.PostAsync($"/api/books/{child}/entries", Body("Ada was a great help today.")));
        Assert.Equal((BookSide.School, true, true), (note.Side, note.IsMine, note.CanEdit));

        // The parent is told, sees one unread note, reads it, and replies with a file.
        Assert.Contains((await parent.GetFromJsonAsync<NotificationInbox>("/api/notifications", Json.Options))!.Items, n => n.Kind == NotificationKind.BookEntry);
        Assert.Equal(1, Assert.Single((await parent.GetFromJsonAsync<List<BookSummary>>("/api/portal/books", Json.Options))!, b => b.StudentId == child).Unread);
        var page = (await parent.GetFromJsonAsync<BookPage>($"/api/books/{child}", Json.Options))!;
        var seen = Assert.Single(page.Entries);
        Assert.Equal((false, false), (seen.IsMine, seen.CanEdit));
        Assert.Equal(HttpStatusCode.NoContent, (await parent.PostAsync($"/api/books/{child}/read", null)).StatusCode);
        Assert.Equal(0, Assert.Single((await parent.GetFromJsonAsync<List<BookSummary>>("/api/portal/books", Json.Options))!, b => b.StudentId == child).Unread);

        var form = Body("Thank you! Her reading list is attached.");
        form.Add(new ByteArrayContent("%PDF-1.4\n% list\n"u8.ToArray()), "file", "reading-list.pdf");
        var reply = await EntryAsync(await parent.PostAsync($"/api/books/{child}/entries", form));
        Assert.Equal((BookSide.Family, "reading-list.pdf"), (reply.Side, reply.Attachment!.FileName));

        // The form teacher sees it unread in the class list and can open the file; the reply is theirs to read, not edit.
        var row = Assert.Single((await s.FormTeacher.GetFromJsonAsync<List<BookSummary>>($"/api/books?classRoomId={s.ClassRoomId}", Json.Options))!, b => b.StudentId == child);
        Assert.Equal(1, row.Unread);
        Assert.Equal(HttpStatusCode.OK, (await s.FormTeacher.GetAsync($"/api/books/{child}/entries/{reply.Id}/file")).StatusCode);
        await (await s.FormTeacher.PutAsJsonAsync($"/api/books/{child}/entries/{reply.Id}", new { body = "changed" })).AssertProblemAsync(HttpStatusCode.Forbidden, "book.not_author");
        var corrected = await EntryAsync(await s.FormTeacher.PutAsJsonAsync($"/api/books/{child}/entries/{note.Id}", new { body = "Ada was a great help in class today." }));
        Assert.NotNull(corrected.EditedAt);
    }

    [Fact]
    public async Task Other_teachers_families_and_students_cannot_open_a_book()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var other = await SchoolBuilder.SetUpAsync(Api);
        var child = s.Students[0];
        (await s.FormTeacher.PostAsync($"/api/books/{child}/entries", Body("Private note"))).EnsureSuccessStatusCode();

        await (await s.SubjectTeacher.GetAsync($"/api/books/{child}")).AssertProblemAsync(HttpStatusCode.NotFound, "book.not_found");
        await (await other.FormTeacher.PostAsync($"/api/books/{child}/entries", Body("Hello"))).AssertProblemAsync(HttpStatusCode.NotFound);
        await (await s.SubjectTeacher.GetAsync($"/api/books?classRoomId={s.ClassRoomId}")).AssertProblemAsync(HttpStatusCode.Forbidden);
        var otherParent = await ParentAsync(other.Students[0]);
        await (await otherParent.GetAsync($"/api/books/{child}")).AssertProblemAsync(HttpStatusCode.NotFound);
        await (await (await Api.ClientAsync(Roles.Bursar)).GetAsync($"/api/books/{child}")).AssertProblemAsync(HttpStatusCode.NotFound);
        await (await s.FormTeacher.PostAsync($"/api/books/{child}/entries", Body(" "))).AssertProblemAsync(HttpStatusCode.BadRequest);

        // An admin can open any book.
        Assert.Single((await (await Api.ClientAsync(Roles.Admin)).GetFromJsonAsync<BookPage>($"/api/books/{child}", Json.Options))!.Entries);
    }

    [Fact]
    public async Task Unread_notes_are_summed_up_in_one_evening_email_a_day()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var email = $"digest-{Guid.NewGuid():N}@example.com";
        await ParentAsync(s.Students[0], email);
        (await s.FormTeacher.PostAsync($"/api/books/{s.Students[0]}/entries", Body("Please sign the trip form."))).EnsureSuccessStatusCode();
        (await s.FormTeacher.PostAsync($"/api/books/{s.Students[0]}/entries", Body("Reading went well today."))).EnsureSuccessStatusCode();

        // Today at 07:00 and 19:00 in Abuja (WAT = UTC+1).
        var watToday = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(1)).Date;
        var morning = new DateTimeOffset(watToday.AddHours(7), TimeSpan.FromHours(1));
        var evening = new DateTimeOffset(watToday.AddHours(19), TimeSpan.FromHours(1));
        Assert.Equal(0, await DigestAsync(morning)); // only from 18:00
        Assert.True(await DigestAsync(evening) >= 1);
        await DispatchAsync();
        var mail = Assert.Single(Api.Email.Sent, m => m.To == email);
        Assert.Contains("2 new notes", mail.TextBody);
        Assert.Contains("Please sign the trip form.", mail.TextBody);

        // Later the same evening: nothing more for this parent (once a day).
        await DigestAsync(evening.AddHours(2));
        await DispatchAsync();
        Assert.Single(Api.Email.Sent, m => m.To == email);
    }

    private async Task<int> DigestAsync(DateTimeOffset now)
    {
        await using var scope = Api.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<ICommandHandler<SendBookDigestCommand, int>>().Handle(new SendBookDigestCommand(now), CancellationToken.None)).Value;
    }

    private async Task DispatchAsync()
    {
        await using var scope = Api.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<EmailDispatcher>();
        while (await dispatcher.DispatchDueAsync(CancellationToken.None) == EmailDispatcher.BatchSize) { }
    }

    // ---------- helpers ----------

    private static MultipartFormDataContent Body(string text) => new() { { new StringContent(text), "body" } };

    private static async Task<BookEntryDto> EntryAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode) Assert.Fail($"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<BookEntryDto>(Json.Options))!;
    }

    /// <summary>A guardian with a portal login for the student.</summary>
    private async Task<HttpClient> ParentAsync(Guid studentId, string? email = null)
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var added = await admin.PostAsJsonAsync($"/api/students/{studentId}/guardians", new
        {
            fullName = "Mrs. Book Reader", phone = $"0805{Interlocked.Increment(ref _phone):0000000}", email = email ?? $"book-{Guid.NewGuid():N}@example.com",
            relationship = "Mother", isPrimaryContact = true, canPickUp = true,
        });
        var guardianId = (await added.Content.ReadFromJsonAsync<GuardianAdded>(Json.Options))!.GuardianId;
        var issued = (await (await admin.PostAsync($"/api/guardians/{guardianId}/login", null)).Content.ReadFromJsonAsync<GuardianLoginIssued>(Json.Options))!;
        var first = await Api.LoginAsync(issued.Credentials!.UserName, issued.Credentials.TemporaryPassword);
        var changed = await Api.CreateClient(first.AccessToken).PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = issued.Credentials.TemporaryPassword, newPassword = "Family#NewPass2026" });
        return Api.CreateClient((await changed.Content.ReadFromJsonAsync<AuthResponse>(Json.Options))!.AccessToken);
    }
}
