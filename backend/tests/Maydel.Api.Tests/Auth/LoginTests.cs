using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Identity;
using Maydel.Domain.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Maydel.Api.Tests.Auth;

/// <summary>E1.1 — password sign-in.</summary>
[Collection(nameof(ApiCollection))]
public sealed class LoginTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task Staff_sign_in_with_email_returns_tokens_and_profile()
    {
        var email = ApiFactory.UniqueEmail();
        var user = await Api.CreateUserAsync(Roles.Teacher, email);

        var auth = await Api.LoginAsync(email);

        Assert.False(string.IsNullOrEmpty(auth.AccessToken));
        Assert.False(string.IsNullOrEmpty(auth.RefreshToken));
        Assert.InRange(auth.AccessTokenExpiresAt - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15.5));
        Assert.InRange(auth.RefreshTokenExpiresAt - DateTimeOffset.UtcNow, TimeSpan.FromDays(13.9), TimeSpan.FromDays(14.1));
        Assert.Equal(user.Id, auth.User.Id);
        Assert.Equal([Role.Teacher], auth.User.Roles);
        Assert.False(auth.User.MustChangePassword);
    }

    [Fact]
    public async Task Student_signs_in_with_admission_number()
    {
        var admissionNo = $"MSS/2026/{Random.Shared.Next(100000, 999999)}";
        await Api.CreateUserAsync(Roles.Student, userName: admissionNo);

        var auth = await Api.LoginAsync(admissionNo);

        Assert.Equal(admissionNo, auth.User.UserName);
        Assert.Equal([Role.Student], auth.User.Roles);
    }

    [Fact]
    public async Task Unknown_user_wrong_password_and_deactivated_account_get_the_identical_response()
    {
        var email = ApiFactory.UniqueEmail();
        await Api.CreateUserAsync(Roles.Parent, email);
        var disabledEmail = ApiFactory.UniqueEmail();
        var disabled = await Api.CreateUserAsync(Roles.Parent, disabledEmail);
        await Api.WithServicesAsync(async sp =>
        {
            var users = sp.GetRequiredService<UserManager<AppUser>>();
            var u = (await users.FindByIdAsync(disabled.Id.ToString()))!;
            u.IsActive = false;
            await users.UpdateAsync(u);
        });

        var client = Api.CreateClient();
        var unknown = await (await client.PostAsJsonAsync("/api/auth/login", new { userName = ApiFactory.UniqueEmail(), password = "Whatever#123" }))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        var wrong = await (await client.PostAsJsonAsync("/api/auth/login", new { userName = email, password = "Wrong#Pass123" }))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        var inactive = await (await client.PostAsJsonAsync("/api/auth/login", new { userName = disabledEmail, password = ApiFactory.TestPassword }))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");

        Assert.Equal(unknown.GetProperty("detail").GetString(), wrong.GetProperty("detail").GetString());
        Assert.Equal(unknown.GetProperty("detail").GetString(), inactive.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Five_failures_lock_the_account_and_even_the_right_password_is_then_refused_generically()
    {
        var email = ApiFactory.UniqueEmail();
        await Api.CreateUserAsync(Roles.Teacher, email);
        var client = Api.CreateClient();

        for (var i = 0; i < 5; i++)
            await (await client.PostAsJsonAsync("/api/auth/login", new { userName = email, password = "Wrong#Pass123" }))
                .AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");

        var locked = await client.PostAsJsonAsync("/api/auth/login", new { userName = email, password = ApiFactory.TestPassword });
        await locked.AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");

        await Api.WithServicesAsync(async sp =>
        {
            var users = sp.GetRequiredService<UserManager<AppUser>>();
            var lockoutEnd = await users.GetLockoutEndDateAsync((await users.FindByEmailAsync(email))!);
            Assert.InRange(lockoutEnd!.Value - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15));
        });
    }

    [Fact]
    public async Task A_successful_sign_in_resets_the_failure_count()
    {
        var email = ApiFactory.UniqueEmail();
        await Api.CreateUserAsync(Roles.Teacher, email);
        var client = Api.CreateClient();

        for (var i = 0; i < 4; i++)
            await client.PostAsJsonAsync("/api/auth/login", new { userName = email, password = "Wrong#Pass123" });
        await Api.LoginAsync(email);
        for (var i = 0; i < 4; i++)
            await client.PostAsJsonAsync("/api/auth/login", new { userName = email, password = "Wrong#Pass123" });

        await Api.LoginAsync(email); // still not locked
    }

    [Fact]
    public async Task Missing_fields_return_400_with_field_errors()
    {
        var response = await Api.CreateClient().PostAsJsonAsync("/api/auth/login", new { userName = "", password = "" });

        var body = await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        var errors = body.GetProperty("errors");
        Assert.True(errors.TryGetProperty("userName", out _));
        Assert.True(errors.TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Sign_in_is_rate_limited_per_ip()
    {
        await using var limited = new ApiFactory(fixture.ConnectionString, new Dictionary<string, string?> { ["RateLimiting:AuthPermitLimit"] = "3" });
        var client = limited.CreateClient();

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await client.PostAsJsonAsync("/api/auth/login", new { userName = "nobody", password = "Wrong#Pass123" })).StatusCode);

        var blocked = await client.PostAsJsonAsync("/api/auth/login", new { userName = "nobody", password = "Wrong#Pass123" });
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }
}
