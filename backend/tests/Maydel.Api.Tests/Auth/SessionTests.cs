using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Abstractions;
using Maydel.Application.Identity;
using Maydel.Domain.Common;
using Maydel.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Maydel.Api.Tests.Auth;

/// <summary>E1.3 — sessions: rotation, reuse detection, logout, password change, deactivation.</summary>
[Collection(nameof(ApiCollection))]
public sealed class SessionTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task Refresh_rotates_the_token_pair()
    {
        var email = ApiFactory.UniqueEmail();
        await Api.CreateUserAsync(Roles.Parent, email);
        var first = await Api.LoginAsync(email);

        var second = await RefreshAsync(first.RefreshToken);

        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.NotEqual(first.AccessToken, second.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await Api.CreateClient(second.AccessToken).GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_revokes_the_whole_family()
    {
        var email = ApiFactory.UniqueEmail();
        await Api.CreateUserAsync(Roles.Parent, email);
        var stolen = await Api.LoginAsync(email);
        var legit = await RefreshAsync(stolen.RefreshToken);
        await BackdateRevocationAsync(stolen.RefreshToken, TimeSpan.FromMinutes(1)); // outside the concurrency grace window

        var replay = await Api.CreateClient().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = stolen.RefreshToken });
        await replay.AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.session_expired");

        // The legitimate holder's newer token is dead too: the thief can't keep the session alive.
        var legitRefresh = await Api.CreateClient().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = legit.RefreshToken });
        await legitRefresh.AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.session_expired");
    }

    [Fact]
    public async Task Concurrent_refreshes_with_the_same_token_within_the_grace_window_both_succeed()
    {
        var email = ApiFactory.UniqueEmail();
        await Api.CreateUserAsync(Roles.Parent, email);
        var session = await Api.LoginAsync(email);

        var a = await RefreshAsync(session.RefreshToken);
        var b = await RefreshAsync(session.RefreshToken);

        Assert.NotEqual(a.RefreshToken, b.RefreshToken);
        await RefreshAsync(a.RefreshToken);
        await RefreshAsync(b.RefreshToken);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var email = ApiFactory.UniqueEmail();
        await Api.CreateUserAsync(Roles.Parent, email);
        var session = await Api.LoginAsync(email);

        var logout = await Api.CreateClient().PostAsJsonAsync("/api/auth/logout", new { refreshToken = session.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refresh = await Api.CreateClient().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = session.RefreshToken });
        await refresh.AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.session_expired");
    }

    [Fact]
    public async Task Logout_with_an_unknown_token_is_still_204()
    {
        var logout = await Api.CreateClient().PostAsJsonAsync("/api/auth/logout", new { refreshToken = "not-a-real-token" });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
    }

    [Fact]
    public async Task Changing_password_signs_out_other_devices_immediately()
    {
        var email = ApiFactory.UniqueEmail();
        await Api.CreateUserAsync(Roles.Teacher, email);
        var phone = await Api.LoginAsync(email);
        var laptop = await Api.LoginAsync(email);

        var change = await Api.CreateClient(laptop.AccessToken).PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = ApiFactory.TestPassword, newPassword = "Brand#NewPass2026" });
        change.EnsureSuccessStatusCode();
        var fresh = (await change.Content.ReadFromJsonAsync<AuthResponse>(Json.Options))!;

        // Old access tokens (both devices) fail on the very next request; the phone's refresh token is revoked.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Api.CreateClient(phone.AccessToken).GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Api.CreateClient(laptop.AccessToken).GetAsync("/api/auth/me")).StatusCode);
        await (await Api.CreateClient().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = phone.RefreshToken }))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.session_expired");

        // The device that changed it carries on with the new pair, and the new password works.
        Assert.Equal(HttpStatusCode.OK, (await Api.CreateClient(fresh.AccessToken).GetAsync("/api/auth/me")).StatusCode);
        await Api.LoginAsync(email, "Brand#NewPass2026");
    }

    [Fact]
    public async Task Change_password_reports_field_errors()
    {
        var email = ApiFactory.UniqueEmail();
        await Api.CreateUserAsync(Roles.Teacher, email);
        var client = Api.CreateClient((await Api.LoginAsync(email)).AccessToken);

        var wrongCurrent = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "Wrong#Pass123", newPassword = "Brand#NewPass2026" });
        var body = await wrongCurrent.AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        Assert.True(body.GetProperty("errors").TryGetProperty("currentPassword", out _));

        var weak = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = ApiFactory.TestPassword, newPassword = "short" });
        body = await weak.AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        Assert.True(body.GetProperty("errors").TryGetProperty("newPassword", out _));
    }

    [Fact]
    public async Task Deactivating_a_user_invalidates_their_access_token_on_the_next_request()
    {
        var email = ApiFactory.UniqueEmail();
        var user = await Api.CreateUserAsync(Roles.Teacher, email);
        var session = await Api.LoginAsync(email);
        var client = Api.CreateClient(session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);

        await Api.WithServicesAsync(async sp =>
        {
            var users = sp.GetRequiredService<UserManager<AppUser>>();
            var u = (await users.FindByIdAsync(user.Id.ToString()))!;
            u.IsActive = false;
            await users.UpdateAsync(u);
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        await (await Api.CreateClient().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = session.RefreshToken }))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.session_expired");
    }

    [Fact]
    public async Task Only_a_hash_of_the_refresh_token_is_stored()
    {
        var email = ApiFactory.UniqueEmail();
        var user = await Api.CreateUserAsync(Roles.Parent, email);
        var session = await Api.LoginAsync(email);

        await Api.WithServicesAsync(async sp =>
        {
            var hashes = await sp.GetRequiredService<AppDbContext>().RefreshTokens
                .Where(t => t.UserId == user.Id).Select(t => t.TokenHash).ToListAsync();
            Assert.DoesNotContain(session.RefreshToken, hashes);
            Assert.Contains(sp.GetRequiredService<ITokenService>().HashRefreshToken(session.RefreshToken), hashes);
        });
    }

    private async Task<AuthResponse> RefreshAsync(string refreshToken)
    {
        var response = await Api.CreateClient().PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(Json.Options))!;
    }

    private Task BackdateRevocationAsync(string refreshToken, TimeSpan by) => Api.WithServicesAsync(async sp =>
    {
        var hash = sp.GetRequiredService<ITokenService>().HashRefreshToken(refreshToken);
        await sp.GetRequiredService<AppDbContext>().RefreshTokens.Where(t => t.TokenHash == hash)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, t => t.RevokedAt - by));
    });
}
