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
    DbSet<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>> UserLogins { get; }
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
    DbSet<FeeSettings> FeeSettings { get; }
    DbSet<OnlinePayment> OnlinePayments { get; }
    DbSet<PaymentProof> PaymentProofs { get; }
    DbSet<InstalmentPlan> InstalmentPlans { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<Payment> Payments { get; }

    DbSet<Announcement> Announcements { get; }
    DbSet<SchoolEvent> Events { get; }
    DbSet<GalleryPhoto> GalleryPhotos { get; }
    DbSet<Testimonial> Testimonials { get; }
    DbSet<NoticeRead> NoticeReads { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<MediaAsset> MediaAssets { get; }
    DbSet<Newsletter> Newsletters { get; }
    DbSet<NewsletterOptOut> NewsletterOptOuts { get; }
    DbSet<Domain.Homework.HomeworkTask> Homework { get; }
    DbSet<Domain.Homework.HomeworkSubmission> HomeworkSubmissions { get; }
    DbSet<Domain.StaffAttendance.ClockSettings> ClockSettings { get; }
    DbSet<Domain.StaffAttendance.StaffDay> StaffDays { get; }

    DbSet<AdmissionApplication> Applications { get; }
    DbSet<AdmissionSettings> AdmissionSettings { get; }
    DbSet<ExamSlot> ExamSlots { get; }
    DbSet<Domain.Cbt.Question> Questions { get; }
    DbSet<Domain.Cbt.CbtTest> CbtTests { get; }
    DbSet<Domain.Cbt.TestAttempt> TestAttempts { get; }
    DbSet<Domain.Cbt.AiGeneration> AiGenerations { get; }
    DbSet<Domain.Cbt.AiSettings> AiSettings { get; }
    DbSet<Domain.Cbt.AiAllowance> AiAllowances { get; }
    DbSet<FormCode> FormCodes { get; }
    DbSet<FormPayment> FormPayments { get; }
    DbSet<ContactMessage> ContactMessages { get; }
    DbSet<NewsletterSubscriber> NewsletterSubscribers { get; }

    DbSet<AuditLog> AuditLogs { get; }

    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>The authenticated caller and their client, resolved per request by the API layer.</summary>
/// <summary>The school's calendar "today" (West Africa Time). Replaced in tests so date rules are deterministic.</summary>
public interface ISchoolClock
{
    DateOnly Today { get; }

    /// <summary>The current instant (UTC). Server time is the source of truth for exam bookings (E11.6).</summary>
    DateTimeOffset Now { get; }
}

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
