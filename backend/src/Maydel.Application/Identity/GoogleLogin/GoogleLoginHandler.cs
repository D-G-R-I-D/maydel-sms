using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Identity.GoogleLogin;

/// <summary>
/// Google only signs in to an account the school already created: it never creates accounts, and students are excluded.
/// The first sign-in links the Google subject; a different Google account for the same email is rejected afterwards.
/// </summary>
internal sealed class GoogleLoginHandler(
    IGoogleIdTokenVerifier google, UserManager<AppUser> users, SessionService sessions, TimeProvider clock)
    : ICommandHandler<GoogleLoginCommand, AuthResponse>
{
    public const string Provider = "Google";

    public async Task<Result<AuthResponse>> Handle(GoogleLoginCommand command, CancellationToken ct)
    {
        if (!google.IsEnabled) return IdentityErrors.GoogleDisabled;

        var identity = await google.VerifyAsync(command.IdToken, ct);
        if (identity is null) return IdentityErrors.GoogleInvalid;
        if (!identity.EmailVerified) return IdentityErrors.GoogleEmailUnverified;

        var normalized = users.NormalizeEmail(identity.Email);
        var matches = await users.Users.Where(u => u.NormalizedEmail == normalized).Take(2).ToListAsync(ct);
        if (matches is not [var user]) return IdentityErrors.GoogleNoAccount;
        if (!user.IsActive || await users.IsInRoleAsync(user, Roles.Student) || await users.IsLockedOutAsync(user))
            return IdentityErrors.GoogleNoAccount;

        var linked = (await users.GetLoginsAsync(user)).Where(l => l.LoginProvider == Provider).ToList();
        if (linked.Count == 0)
        {
            // One Google account can't be linked to two school accounts.
            if (await users.FindByLoginAsync(Provider, identity.Subject) is not null) return IdentityErrors.GoogleAccountMismatch;
            var added = await users.AddLoginAsync(user, new UserLoginInfo(Provider, identity.Subject, Provider));
            if (!added.Succeeded) throw new InvalidOperationException("Could not link Google login: " + added);
        }
        else if (linked.All(l => l.ProviderKey != identity.Subject))
        {
            return IdentityErrors.GoogleAccountMismatch;
        }

        user.LastLoginAt = clock.GetUtcNow();
        await users.UpdateAsync(user);
        return await sessions.IssueAsync(user, ct: ct);
    }
}
