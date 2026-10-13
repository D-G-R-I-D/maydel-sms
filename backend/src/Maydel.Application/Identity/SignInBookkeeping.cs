using Maydel.Application.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Identity;

/// <summary>
/// Sign-in bookkeeping as single atomic SQL statements. UserManager would load the user and save it back under the
/// optimistic-concurrency stamp, so two overlapping sign-ins collided (409), and parallel wrong passwords could lose
/// increments and dodge lockout. These statements don't touch the stamp and can't lose an increment.
/// (Not written to the audit log: they are sign-in events, not data changes.)
/// </summary>
public static class SignInBookkeeping
{
    /// <summary>Counts a failed attempt; on reaching the limit, locks the account and resets the count (as Identity does).</summary>
    public static async Task RecordFailedSignInAsync(this IAppDbContext db, Guid userId, LockoutOptions lockout, TimeProvider clock, CancellationToken ct)
    {
        await db.Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.AccessFailedCount, u => u.AccessFailedCount + 1), ct);

        var lockedUntil = clock.GetUtcNow().Add(lockout.DefaultLockoutTimeSpan);
        await db.Users.Where(u => u.Id == userId && u.LockoutEnabled && u.AccessFailedCount >= lockout.MaxFailedAccessAttempts)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.LockoutEnd, lockedUntil)
                .SetProperty(u => u.AccessFailedCount, 0), ct);
    }

    /// <summary>Clears the failure count and stamps the sign-in time.</summary>
    public static Task RecordSuccessfulSignInAsync(this IAppDbContext db, Guid userId, TimeProvider clock, CancellationToken ct) =>
        db.Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.AccessFailedCount, 0)
                .SetProperty(u => u.LastLoginAt, clock.GetUtcNow()), ct);
}
