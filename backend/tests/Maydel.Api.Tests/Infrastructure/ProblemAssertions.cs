using System.Net;
using System.Text.Json;

namespace Maydel.Api.Tests.Infrastructure;

public static class ProblemAssertions
{
    /// <summary>Asserts an RFC 9457 problem response with the given status (and error code, when given). Returns the body.</summary>
    public static async Task<JsonElement> AssertProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string? code = null)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal((int)status, body.GetProperty("status").GetInt32());
        Assert.True(body.TryGetProperty("traceId", out _), "Problem responses must carry a traceId.");
        if (code is not null) Assert.Equal(code, body.GetProperty("code").GetString());
        return body;
    }
}
