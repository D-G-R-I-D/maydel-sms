using Maydel.Application.Identity;
using Maydel.Domain.Academics;
using Maydel.Domain.Admissions;
using Maydel.Domain.Assessment;
using Maydel.Domain.Attendance;
using Maydel.Domain.Audit;
using Maydel.Domain.Communication;
using Maydel.Domain.Finance;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Maydel.Application.Abstractions;

/// <summary>The EF Core unit of work. Handlers use it directly; there is no repository layer (ADR-004).</summary>
public interface IAppDbContext
{
    DbSet<AppUser> Users { get; }
    DbSet<AppRole> Roles { get; }
    DbSet<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>> UserRoles { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<AcademicSession> Sessions { get; }
    DbSet<Term> Terms { get; }
    DbSet<ClassLevel> ClassLevels { get; }
    DbSet<ClassRoom> ClassRooms { get; }
    DbSet<Subject> Subjects { get; }
    DbSet<ClassSubject> ClassSubjects { get; }

    DbSet<Student> Students { get; }
    DbSet<Guardian> Guardians { get; }
    DbSet<StudentGuardian> StudentGuardians { get; }
    DbSet<Staff> Staff { get; }

    DbSet<AssessmentComponent> AssessmentComponents { get; }
    DbSet<GradeBand> GradeBands { get; }
    DbSet<SubjectScore> SubjectScores { get; }
    DbSet<TermReport> TermReports { get; }
    DbSet<BehaviourTrait> BehaviourTraits { get; }
    DbSet<OutboxEmail> OutboxEmails { get; }
    DbSet<Domain.Settings.SchoolProfile> SchoolProfiles { get; }

    DbSet<AttendanceRecord> Attendance { get; }

    DbSet<FeeItem> FeeItems { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<Payment> Payments { get; }

    DbSet<Announcement> Announcements { get; }
    DbSet<SchoolEvent> Events { get; }
    DbSet<GalleryPhoto> GalleryPhotos { get; }

    DbSet<AdmissionApplication> Applications { get; }
    DbSet<ContactMessage> ContactMessages { get; }
    DbSet<NewsletterSubscriber> NewsletterSubscribers { get; }

    DbSet<AuditLog> AuditLogs { get; }

    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>The authenticated caller and their client, resolved per request by the API layer.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    string? UserName { get; }
    bool IsInRole(string role);

    /// <summary>Client IP (the browser's, forwarded by the BFF from a trusted proxy).</summary>
    string? IpAddress { get; }
    string? UserAgent { get; }
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>Token cryptography. Session rules (rotation, reuse detection) live in <see cref="Identity.SessionService"/>.</summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(AppUser user, IEnumerable<string> roles);

    /// <summary>A new opaque refresh token (64 random bytes, base64).</summary>
    string CreateRefreshToken();

    /// <summary>One-way hash of a refresh token; only the hash is stored.</summary>
    string HashRefreshToken(string refreshToken);

    TimeSpan RefreshTokenLifetime { get; }
}

public sealed record GoogleIdentity(string Subject, string Email, bool EmailVerified);

public interface IGoogleIdTokenVerifier
{
    /// <summary>False when no Google client id is configured.</summary>
    bool IsEnabled { get; }

    /// <summary>Verifies signature, audience, issuer and expiry. Returns null for any invalid token.</summary>
    Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken ct);
}
