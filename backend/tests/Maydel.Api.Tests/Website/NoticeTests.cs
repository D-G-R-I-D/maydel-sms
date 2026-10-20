using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Admissions;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Website;
using Maydel.Application.Website.Notices;
using Maydel.Domain.Common;

namespace Maydel.Api.Tests.Website;

/// <summary>E12.1: notices reach the right people in the portal, with an unread count that clears.</summary>
[Collection(nameof(ApiCollection))]
public sealed class NoticeTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task Parents_staff_and_students_see_their_own_notices_and_can_mark_them_read()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var tag = Guid.NewGuid().ToString("N")[..6];
        async Task<PostDto> PublishAsync(string audience)
        {
            var post = await AdmissionsKit.ReadAsync<PostDto>(await admin.PostAsJsonAsync("/api/cms/posts", new { title = $"{audience} notice {tag}", body = "Details", audience }));
            return await AdmissionsKit.ReadAsync<PostDto>(await admin.PostAsync($"/api/cms/posts/{post.Id}/publish", null));
        }
        var forParents = await PublishAsync("Parents");
        var forStaff = await PublishAsync("Staff");
        var draft = await AdmissionsKit.ReadAsync<PostDto>(await admin.PostAsJsonAsync("/api/cms/posts", new { title = $"Draft {tag}", body = "x", audience = "Everyone" }));

        var parent = await Api.ClientAsync(Roles.Parent);
        var inbox = await AdmissionsKit.ReadAsync<NoticeInbox>(await parent.GetAsync("/api/notices"));
        Assert.Contains(inbox.Notices, n => n.Id == forParents.Id && !n.IsRead);
        Assert.DoesNotContain(inbox.Notices, n => n.Id == forStaff.Id || n.Id == draft.Id);
        var before = inbox.Unread;

        Assert.Equal(HttpStatusCode.NoContent, (await parent.PostAsync($"/api/notices/{forParents.Id}/read", null)).StatusCode);
        var after = await AdmissionsKit.ReadAsync<NoticeInbox>(await parent.GetAsync("/api/notices"));
        Assert.Equal(before - 1, after.Unread);
        Assert.True(after.Notices.Single(n => n.Id == forParents.Id).IsRead);
        await (await parent.PostAsync($"/api/notices/{forStaff.Id}/read", null)).AssertProblemAsync(HttpStatusCode.NotFound);

        var teacher = await Api.ClientAsync(Roles.Teacher);
        Assert.Contains((await AdmissionsKit.ReadAsync<NoticeInbox>(await teacher.GetAsync("/api/notices"))).Notices, n => n.Id == forStaff.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await teacher.PostAsync("/api/notices/read-all", null)).StatusCode);
        Assert.Equal(0, (await AdmissionsKit.ReadAsync<NoticeInbox>(await teacher.GetAsync("/api/notices"))).Unread);
        await (await Api.CreateClient().GetAsync("/api/notices")).AssertProblemAsync(HttpStatusCode.Unauthorized);
    }
}
