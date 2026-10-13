using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Identity.Login;

// Non-transactional: a failed attempt must still be counted towards lockout.
internal sealed class LoginHandler(UserManager<AppUser> users, IAppDbContext db, SessionService sessions, TimeProvider clock)
    : ICommandHandler<LoginCommand, AuthResponse>
{
    private static readonly AppUser DummyUser = new() { FullName = "", UserName = "" };
    private static readonly Lazy<string> DummyHash = new(() => new PasswordHasher<AppUser>().HashPassword(DummyUser, Guid.NewGuid().ToString()));

    public async Task<Result<AuthResponse>> Handle(LoginCommand command, CancellationToken ct)
    {
        var user = await FindAsync(command.UserName.Trim(), ct);

        if (user is null || !user.IsActive || await users.IsLockedOutAsync(user))
        {
            // Spend the same time hashing as a real check, so response timing doesn't reveal which accounts exist.
            users.PasswordHasher.VerifyHashedPassword(DummyUser, DummyHash.Value, command.Password);
            return IdentityErrors.InvalidCredentials;
        }

        if (!await users.CheckPasswordAsync(user, command.Password))
        {
            await db.RecordFailedSignInAsync(user.Id, users.Options.Lockout, clock, ct);
            return IdentityErrors.InvalidCredentials;
        }

        await db.RecordSuccessfulSignInAsync(user.Id, clock, ct);
        return await sessions.IssueAsync(user, ct: ct);
    }

    /// <summary>Staff and parents sign in with email; students with their admission number (the username).</summary>
    private async Task<AppUser?> FindAsync(string userName, CancellationToken ct)
    {
        if (await users.FindByNameAsync(userName) is { } byName) return byName;

        // Emails are not unique across all accounts (students have none), so only accept an unambiguous match.
        var normalized = users.NormalizeEmail(userName);
        var matches = await users.Users.Where(u => u.NormalizedEmail == normalized).Take(2).ToListAsync(ct);
        return matches.Count == 1 ? matches[0] : null;
    }
}
