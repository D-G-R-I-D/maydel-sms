using Google.Apis.Auth;
using Maydel.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Maydel.Infrastructure.Identity;

public sealed class GoogleAuthOptions
{
    public const string Section = "Authentication:Google";

    /// <summary>OAuth client id of the web app. Leave empty to disable Google sign-in.</summary>
    public string? ClientId { get; set; }
}

internal sealed class GoogleIdTokenVerifier(IOptions<GoogleAuthOptions> options, ILogger<GoogleIdTokenVerifier> logger) : IGoogleIdTokenVerifier
{
    public bool IsEnabled => !string.IsNullOrWhiteSpace(options.Value.ClientId);

    public async Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken ct)
    {
        try
        {
            // Checks the signature against Google's keys, plus issuer, expiry and that the audience is our client id.
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [options.Value.ClientId!] });
            return new GoogleIdentity(payload.Subject, payload.Email, payload.EmailVerified);
        }
        catch (InvalidJwtException ex)
        {
            logger.LogInformation("Rejected Google ID token: {Reason}", ex.Message);
            return null;
        }
    }
}
