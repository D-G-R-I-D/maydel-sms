using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Microsoft.Extensions.Options;

namespace Maydel.Api.Tests.Platform;

/// <summary>E0.3 configuration and E0.4 observability.</summary>
[Collection(nameof(ApiCollection))]
public sealed class PlatformTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task Liveness_and_readiness_are_public()
    {
        Assert.Equal(HttpStatusCode.OK, (await Api.CreateClient().GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Api.CreateClient().GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task Unhandled_exception_returns_500_problem_with_trace_id_and_no_internals()
    {
        var response = await Api.CreateClient().PostAsJsonAsync("/api/auth/google", new { idToken = FakeGoogleVerifier.ExplodingToken });

        var body = await response.AssertProblemAsync(HttpStatusCode.InternalServerError);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
        var raw = body.GetRawText();
        Assert.DoesNotContain("hunter2", raw);
        Assert.DoesNotContain("InvalidOperationException", raw);
        Assert.DoesNotContain(" at ", raw); // no stack frames
    }

    [Fact]
    public async Task Responses_carry_security_headers()
    {
        var response = await Api.CreateClient().GetAsync("/health");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("default-src 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Theory]
    [InlineData("")]
    [InlineData("too-short-key")]
    public void App_refuses_to_start_without_a_strong_signing_key(string key)
    {
        using var api = new ApiFactory(fixture.ConnectionString, new Dictionary<string, string?> { ["Jwt:SigningKey"] = key });

        var error = Assert.ThrowsAny<Exception>(() => api.CreateClient());

        var validation = error as OptionsValidationException ?? error.InnerException as OptionsValidationException;
        Assert.NotNull(validation);
        Assert.Contains("Jwt:SigningKey", validation.Message);
    }
}
