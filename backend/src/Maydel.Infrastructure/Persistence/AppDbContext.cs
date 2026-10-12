using Maydel.Application.Abstractions;
using Maydel.Domain.Academics;
using Maydel.Domain.Admissions;
using Maydel.Domain.Assessment;
using Maydel.Domain.Attendance;
using Maydel.Domain.Audit;
using Maydel.Domain.Communication;
using Maydel.Domain.Finance;
using Maydel.Domain.People;
using Maydel.Application.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, AppRole, Guid>(options), IAppDbContext
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<AcademicSession> Sessions => Set<AcademicSession>();
    public DbSet<Term> Terms => Set<Term>();
    public DbSet<ClassLevel> ClassLevels => Set<ClassLevel>();
    public DbSet<ClassRoom> ClassRooms => Set<ClassRoom>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<ClassSubject> ClassSubjects => Set<ClassSubject>();

    public DbSet<Student> Students => Set<Student>();
    public DbSet<Guardian> Guardians => Set<Guardian>();
    public DbSet<StudentGuardian> StudentGuardians => Set<StudentGuardian>();
    public DbSet<Staff> Staff => Set<Staff>();

    public DbSet<AssessmentComponent> AssessmentComponents => Set<AssessmentComponent>();
    public DbSet<GradeBand> GradeBands => Set<GradeBand>();
    public DbSet<SubjectScore> SubjectScores => Set<SubjectScore>();
    public DbSet<TermReport> TermReports => Set<TermReport>();

    public DbSet<AttendanceRecord> Attendance => Set<AttendanceRecord>();

    public DbSet<FeeItem> FeeItems => Set<FeeItem>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<SchoolEvent> Events => Set<SchoolEvent>();
    public DbSet<GalleryPhoto> GalleryPhotos => Set<GalleryPhoto>();

    public DbSet<AdmissionApplication> Applications => Set<AdmissionApplication>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<NewsletterSubscriber> NewsletterSubscribers => Set<NewsletterSubscriber>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(12, 2);
        configurationBuilder.Properties<string>().HaveMaxLength(256);
    }
}
