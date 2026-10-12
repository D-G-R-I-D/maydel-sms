using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Identity;
using Maydel.Domain.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Maydel.Api.Tests.Auth;

/// <summary>E1.2 — "Continue with Google" (the Google signature check itself is faked; see GoogleIdTokenVerifier).</summary>
[Collection(nameof(ApiCollection))]
public sealed class GoogleLoginTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task Existing_parent_signs_in_and_the_google_account_is_linked()
    {
        var email = ApiFactory.UniqueEmail();
        var user = await Api.CreateUserAsync(Roles.Parent, email);

        var response = await PostAsync(Api.Google.Issue(email, subject: "sub-1"));

        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json.Options))!;
        Assert.Equal(user.Id, auth.User.Id);
        await Api.WithServicesAsync(async sp =>
        {
            var logins = await sp.GetRequiredService<UserManager<AppUser>>().GetLoginsAsync(user);
            Assert.Contains(logins, l => l is { LoginProvider: "Google", ProviderKey: "sub-1" });
        });
    }

    [Fact]
    public async Task Email_match_is_case_insensitive()
    {
        var email = ApiFactory.UniqueEmail();
        await Api.CreateUserAsync(Roles.Teacher, email);

        (await PostAsync(Api.Google.Issue(email.ToUpperInvariant()))).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Unknown_email_never_creates_an_account()
    {
        var email = ApiFactory.UniqueEmail();

        await (await PostAsync(Api.Google.Issue(email))).AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.google_no_account");

        await Api.WithServicesAsync(async sp =>
            Assert.False(await sp.GetRequiredService<UserManager<AppUser>>().Users.AnyAsync(u => u.Email == email)));
    }

    [Fact]
    public async Task Students_cannot_use_google()
    {
        var email = ApiFactory.UniqueEmail();
        await Api.CreateUserAsync(Roles.Student, email);

        await (await PostAsync(Api.Google.Issue(email))).AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.google_no_account");
    }

    [Fact]
    public async Task Unverified_google_email_is_rejected()
    {
        var email = ApiFactory.UniqueEmail();
        await Api.CreateUserAsync(Roles.Parent, email);

        await (await PostAsync(Api.Google.Issue(email, emailVerified: false)))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.google_email_unverified");
    }

    [Fact]
    public async Task Invalid_token_is_rejected()
    {
        await (await PostAsync("forged-token")).AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.google_invalid");
    }

    [Fact]
    public async Task After_linking_a_different_google_account_for_the_same_email_is_rejected()
    {
        var email = ApiFactory.UniqueEmail();
        await Api.CreateUserAsync(Roles.Parent, email);
        (await PostAsync(Api.Google.Issue(email, subject: "original"))).EnsureSuccessStatusCode();

        await (await PostAsync(Api.Google.Issue(email, subject: "impostor")))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.google_mismatch");
        (await PostAsync(Api.Google.Issue(email, subject: "original"))).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Deactivated_account_cannot_sign_in_with_google()
    {
        var email = ApiFactory.UniqueEmail();
        var user = await Api.CreateUserAsync(Roles.Parent, email);
        await Api.WithServicesAsync(async sp =>
        {
            var users = sp.GetRequiredService<UserManager<AppUser>>();
            var u = (await users.FindByIdAsync(user.Id.ToString()))!;
            u.IsActive = false;
            await users.UpdateAsync(u);
        });

        await (await PostAsync(Api.Google.Issue(email))).AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.google_no_account");
    }

    [Fact]
    public async Task Returns_404_when_google_sign_in_is_not_configured()
    {
        await using var api = new ApiFactory(fixture.ConnectionString);
        api.Google.IsEnabled = false;

        var response = await api.CreateClient().PostAsJsonAsync("/api/auth/google", new { idToken = "anything" });

        await response.AssertProblemAsync(HttpStatusCode.NotFound, "auth.google_disabled");
    }

    private Task<HttpResponseMessage> PostAsync(string idToken) =>
        Api.CreateClient().PostAsJsonAsync("/api/auth/google", new { idToken });
}
