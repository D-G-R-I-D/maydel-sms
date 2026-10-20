using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Admissions;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Common;
using Maydel.Application.Identity;
using Maydel.Application.Users;
using Maydel.Domain.Common;

namespace Maydel.Api.Tests.Users;

/// <summary>Settings → Users: list every login, disable/enable families and students, reset passwords; staff stay on the staff record.</summary>
[Collection(nameof(ApiCollection))]
public sealed class UserTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task Admins_list_logins_disable_and_enable_families_and_reset_passwords()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var email = ApiFactory.UniqueEmail();
        await Api.CreateUserAsync(Roles.Parent, email);
        var page = await AdmissionsKit.ReadAsync<PagedResult<UserRow>>(await admin.GetAsync($"/api/users?search={Uri.EscapeDataString(email)}"));
        var parent = Assert.Single(page.Items);
        Assert.Equal((true, true), (parent.IsActive, parent.Roles.Contains("Parent")));

        var disabled = await AdmissionsKit.ReadAsync<UserRow>(await admin.PostAsync($"/api/users/{parent.Id}/disable", null));
        Assert.False(disabled.IsActive);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Api.CreateClient().PostAsJsonAsync("/api/auth/login", new { userName = email, password = ApiFactory.TestPassword })).StatusCode);
        await (await admin.PostAsync($"/api/users/{parent.Id}/reset-password", null)).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "user.inactive");

        Assert.True((await AdmissionsKit.ReadAsync<UserRow>(await admin.PostAsync($"/api/users/{parent.Id}/enable", null))).IsActive);
        var issued = await AdmissionsKit.ReadAsync<IssuedCredentials>(await admin.PostAsync($"/api/users/{parent.Id}/reset-password", null));
        var signIn = await Api.LoginAsync(email, issued.TemporaryPassword);
        Assert.True(signIn.User.MustChangePassword);

        // Staff go through their staff record; nobody can lock themselves out; teachers have no access.
        var (_, staff) = await SchoolBuilder.TeacherAsync(Api);
        var staffRow = Assert.Single((await AdmissionsKit.ReadAsync<PagedResult<UserRow>>(await admin.GetAsync($"/api/users?kind=Staff&search={Uri.EscapeDataString(staff.Email)}"))).Items);
        Assert.Equal((AccountKind.Staff, staff.Id), (staffRow.Kind, staffRow.LinkedId!.Value));
        await (await admin.PostAsync($"/api/users/{staffRow.Id}/disable", null)).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "user.staff");
        var me = await AdmissionsKit.ReadAsync<MeResponse>(await admin.GetAsync("/api/auth/me"));
        await (await admin.PostAsync($"/api/users/{me.Id}/reset-password", null)).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "user.self");
        await (await (await Api.ClientAsync(Roles.Teacher)).GetAsync("/api/users")).AssertProblemAsync(HttpStatusCode.Forbidden);
    }
}
