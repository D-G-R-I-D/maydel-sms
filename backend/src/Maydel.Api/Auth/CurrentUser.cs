using System.Security.Claims;
using Maydel.Application.Abstractions;
using Maydel.Infrastructure.Identity;

namespace Maydel.Api.Auth;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private HttpContext? Http => accessor.HttpContext;

    public Guid? UserId => Guid.TryParse(Http?.User.FindFirstValue(AppClaims.Subject), out var id) ? id : null;
    public string? UserName => Http?.User.FindFirstValue(AppClaims.UserName);
    public bool IsInRole(string role) => Http?.User.IsInRole(role) ?? false;

    // RemoteIpAddress is the browser's address once UseForwardedHeaders has accepted X-Forwarded-For from a trusted proxy (the BFF).
    public string? IpAddress => Http?.Connection.RemoteIpAddress?.ToString();
    public string? UserAgent => Http?.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;
}
