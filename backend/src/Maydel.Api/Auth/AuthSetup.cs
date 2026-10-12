using Maydel.Application.Identity;
using Maydel.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Maydel.Api.Auth;

public static class AuthSetup
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((o, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = jwt.Key(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = AppClaims.UserName,
                    RoleClaimType = AppClaims.Role,
                };
                o.Events = new JwtBearerEvents { OnTokenValidated = RejectStaleSessionsAsync };
            });
        return services;
    }

    /// <summary>
    /// Rejects tokens of deactivated users, and tokens issued before a password change, role change or admin sign-out
    /// (each rotates the security stamp). This makes revocation take effect on the very next request (E1.3).
    /// </summary>
    private static async Task RejectStaleSessionsAsync(TokenValidatedContext ctx)
    {
        var users = ctx.HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();
        var userId = ctx.Principal?.FindFirst(AppClaims.Subject)?.Value;
        var user = userId is null ? null : await users.FindByIdAsync(userId);
        if (user is null || !user.IsActive || user.SecurityStamp != ctx.Principal!.FindFirst(AppClaims.SecurityStamp)?.Value)
            ctx.Fail("Session is no longer valid.");
    }
}
