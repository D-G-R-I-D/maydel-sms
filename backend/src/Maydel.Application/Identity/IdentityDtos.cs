using Maydel.Domain.Common;

namespace Maydel.Application.Identity;

/// <summary>
/// Returned by every sign-in style call. The BFF stores both tokens in httpOnly cookies and never exposes them to browser JavaScript.
/// </summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    MeResponse User);

public sealed record MeResponse(
    Guid Id,
    string UserName,
    string FullName,
    string? Email,
    IReadOnlyList<Role> Roles,
    bool MustChangePassword);

public static class IdentityErrors
{
    // One message for unknown user, wrong password, disabled and locked accounts, so accounts can't be enumerated.
    public static readonly Common.Error InvalidCredentials = Common.Error.Unauthorized("auth.invalid_credentials",
        "Invalid username or password. After 5 failed attempts, sign-in is paused for 15 minutes.");

    public static readonly Common.Error SessionExpired = Common.Error.Unauthorized("auth.session_expired",
        "Your session has expired. Please sign in again.");

    public static readonly Common.Error GoogleDisabled = Common.Error.NotFound("auth.google_disabled",
        "Google sign-in is not enabled.");

    public static readonly Common.Error GoogleInvalid = Common.Error.Unauthorized("auth.google_invalid",
        "Google sign-in failed. Please try again.");

    public static readonly Common.Error GoogleEmailUnverified = Common.Error.Unauthorized("auth.google_email_unverified",
        "Your Google email address is not verified.");

    public static readonly Common.Error GoogleNoAccount = Common.Error.Unauthorized("auth.google_no_account",
        "No school account is linked to this Google address. Contact the school office.");

    public static readonly Common.Error GoogleAccountMismatch = Common.Error.Unauthorized("auth.google_mismatch",
        "This school account is linked to a different Google account.");

    public static readonly Common.Error PasswordChangeRequired = Common.Error.Forbidden("auth.password_change_required",
        "You must change your temporary password before continuing.");
}
