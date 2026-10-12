using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Identity;
using Maydel.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Maydel.Api.Tests.Auth;

/// <summary>E1.4 first-login gate and E1.6 role policies.</summary>
[Collection(nameof(ApiCollection))]
public sealed class AuthorizationTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task User_with_temporary_password_can_only_reach_auth_endpoints_until_they_change_it()
    {
        var email = ApiFactory.UniqueEmail();
        await Api.CreateUserAsync(Roles.Admin, email, mustChangePassword: true);
        var session = await Api.LoginAsync(email);
        Assert.True(session.User.MustChangePassword);
        var client = Api.CreateClient(session.AccessToken);

        await (await client.GetAsync("/api/audit")).AssertProblemAsync(HttpStatusCode.Forbidden, "auth.password_change_required");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);

        var change = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = ApiFactory.TestPassword, newPassword = "Brand#NewPass2026" });
        change.EnsureSuccessStatusCode();
        var fresh = (await change.Content.ReadFromJsonAsync<AuthResponse>(Json.Options))!;

        Assert.False(fresh.User.MustChangePassword);
        Assert.Equal(HttpStatusCode.OK, (await Api.CreateClient(fresh.AccessToken).GetAsync("/api/audit")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_requests_to_protected_endpoints_get_401_problem()
    {
        await (await Api.CreateClient().GetAsync("/api/auth/me")).AssertProblemAsync(HttpStatusCode.Unauthorized);
        await (await Api.CreateClient().GetAsync("/api/audit")).AssertProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Tampered_token_is_rejected()
    {
        var email = ApiFactory.UniqueEmail();
        await Api.CreateUserAsync(Roles.Teacher, email);
        var token = (await Api.LoginAsync(email)).AccessToken;
        var tampered = token[..^4] + (token.EndsWith("AAAA") ? "BBBB" : "AAAA");

        Assert.Equal(HttpStatusCode.Unauthorized, (await Api.CreateClient(tampered).GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData(Roles.Teacher)]
    [InlineData(Roles.Bursar)]
    [InlineData(Roles.Parent)]
    [InlineData(Roles.Student)]
    public async Task Only_admins_can_read_the_audit_log(string role)
    {
        var email = ApiFactory.UniqueEmail();
        await Api.CreateUserAsync(role, email);
        var client = Api.CreateClient((await Api.LoginAsync(email)).AccessToken);

        await (await client.GetAsync("/api/audit")).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await client.GetAsync("/api/audit/filters")).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public void Every_endpoint_declares_authorization_and_anonymous_ones_live_under_allowed_prefixes()
    {
        string[] anonymousPrefixes = ["/api/public", "/api/auth", "/api/files", "/api/webhooks", "/health", "/openapi", "/scalar"];
        var endpoints = Api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();
        Assert.NotEmpty(endpoints);

        foreach (var endpoint in endpoints)
        {
            var route = "/" + endpoint.RoutePattern.RawText!.TrimStart('/');
            var anonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            var authorized = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0;

            if (anonymous)
                Assert.True(anonymousPrefixes.Any(p => route.StartsWith(p, StringComparison.OrdinalIgnoreCase)), $"{route} allows anonymous access outside the allowed prefixes.");
            else
                Assert.True(authorized, $"{route} does not declare an authorization policy.");
        }
    }
}
