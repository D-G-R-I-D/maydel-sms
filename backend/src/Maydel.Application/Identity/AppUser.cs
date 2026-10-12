using Microsoft.AspNetCore.Identity;

namespace Maydel.Application.Identity;

public class AppUser : IdentityUser<Guid>
{
    public AppUser()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString();
    }

    public required string FullName { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Set for accounts created by staff with a temporary password.</summary>
    public bool MustChangePassword { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; set; }
}

public class AppRole : IdentityRole<Guid>
{
    public AppRole() => Id = Guid.CreateVersion7();

    public AppRole(string name) : this() => Name = name;
}

public enum RefreshTokenRevocation
{
    /// <summary>Exchanged for a new token in the same family (normal rotation).</summary>
    Rotated,
    Logout,

    /// <summary>A revoked token was presented again outside the grace window: assume theft.</summary>
    ReuseDetected,

    /// <summary>Password change or admin action signed the user out everywhere.</summary>
    SignedOutEverywhere,
}

/// <summary>
/// Rotating refresh token. Only a SHA-256 hash is stored, so a database leak does not leak sessions.
/// Reuse of a revoked token revokes the whole family (theft detection).
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public required string TokenHash { get; set; }
    public Guid FamilyId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public RefreshTokenRevocation? RevokedReason { get; set; }
    public string? CreatedByIp { get; set; }
    public string? UserAgent { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}
