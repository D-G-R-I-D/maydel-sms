using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Identity;

/// <summary>
/// Session rules shared by the sign-in slices: issuing token pairs, rotating refresh tokens with reuse detection, and revocation.
/// </summary>
public sealed class SessionService(IAppDbContext db, UserManager<AppUser> users, ITokenService tokens, ICurrentUser client, TimeProvider clock)
{
    /// <summary>
    /// A rotated token presented again within this window is treated as a concurrent refresh from the same client
    /// (e.g. two server requests racing), not theft. Outside it, reuse revokes the whole family.
    /// </summary>
    public static readonly TimeSpan ReuseGracePeriod = TimeSpan.FromSeconds(20);

    public async Task<AuthResponse> IssueAsync(AppUser user, Guid? familyId = null, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var roles = await users.GetRolesAsync(user);
        var access = tokens.CreateAccessToken(user, roles);

        var refresh = tokens.CreateRefreshToken();
        var refreshExpires = now.Add(tokens.RefreshTokenLifetime);
        var userAgent = client.UserAgent;
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokens.HashRefreshToken(refresh),
            FamilyId = familyId ?? Guid.CreateVersion7(),
            CreatedAt = now,
            ExpiresAt = refreshExpires,
            CreatedByIp = client.IpAddress,
            UserAgent = userAgent?.Length > 512 ? userAgent[..512] : userAgent,
        });
        await db.SaveChangesAsync(ct);

        return new AuthResponse(access.Token, access.ExpiresAt, refresh, refreshExpires, ToMe(user, roles));
    }

    /// <summary>
    /// Exchanges a refresh token for a new pair in the same family. Presenting a token that was already revoked means it was
    /// stolen and replayed, so every token in the family is revoked and the user must sign in again.
    /// </summary>
    public async Task<Result<AuthResponse>> RotateAsync(string refreshToken, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var hash = tokens.HashRefreshToken(refreshToken);
        var stored = await db.RefreshTokens.Include(t => t.User).SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null) return IdentityErrors.SessionExpired;

        if (stored.RevokedAt is { } revokedAt)
        {
            var concurrentRefresh = stored.RevokedReason == RefreshTokenRevocation.Rotated && now - revokedAt <= ReuseGracePeriod;
            if (!concurrentRefresh)
            {
                if (stored.RevokedReason == RefreshTokenRevocation.Rotated)
                    await RevokeFamilyAsync(stored.FamilyId, RefreshTokenRevocation.ReuseDetected, ct);
                return IdentityErrors.SessionExpired;
            }
        }
        else if (stored.ExpiresAt <= now)
        {
            return IdentityErrors.SessionExpired;
        }

        if (!stored.User.IsActive || await users.IsLockedOutAsync(stored.User)) return IdentityErrors.SessionExpired;

        if (stored.RevokedAt is null)
        {
            stored.RevokedAt = now;
            stored.RevokedReason = RefreshTokenRevocation.Rotated;
        }
        return await IssueAsync(stored.User, stored.FamilyId, ct);
    }

    /// <summary>Signs out the device that holds this refresh token. Unknown tokens are ignored.</summary>
    public async Task RevokeAsync(string refreshToken, CancellationToken ct)
    {
        var hash = tokens.HashRefreshToken(refreshToken);
        var familyId = await db.RefreshTokens.Where(t => t.TokenHash == hash).Select(t => (Guid?)t.FamilyId).SingleOrDefaultAsync(ct);
        if (familyId is { } id) await RevokeFamilyAsync(id, RefreshTokenRevocation.Logout, ct);
    }

    public Task RevokeAllForUserAsync(Guid userId, CancellationToken ct) =>
        db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, clock.GetUtcNow())
                .SetProperty(t => t.RevokedReason, RefreshTokenRevocation.SignedOutEverywhere), ct);

    public static MeResponse ToMe(AppUser user, IEnumerable<string> roles) =>
        new(user.Id, user.UserName!, user.FullName, user.Email, [.. roles.Order()], user.MustChangePassword);

    private Task RevokeFamilyAsync(Guid familyId, RefreshTokenRevocation reason, CancellationToken ct) =>
        db.RefreshTokens.Where(t => t.FamilyId == familyId && (t.RevokedAt == null || t.RevokedReason == RefreshTokenRevocation.Rotated))
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, t => t.RevokedAt ?? clock.GetUtcNow())
                .SetProperty(t => t.RevokedReason, reason), ct);
}
