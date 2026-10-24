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
    public DbSet<BehaviourTrait> BehaviourTraits => Set<BehaviourTrait>();
    public DbSet<OutboxEmail> OutboxEmails => Set<OutboxEmail>();
    public DbSet<Domain.Settings.SchoolProfile> SchoolProfiles => Set<Domain.Settings.SchoolProfile>();

    public DbSet<AttendanceRecord> Attendance => Set<AttendanceRecord>();

    public DbSet<FeeItem> FeeItems => Set<FeeItem>();
    public DbSet<FeeSettings> FeeSettings => Set<FeeSettings>();
    public DbSet<OnlinePayment> OnlinePayments => Set<OnlinePayment>();
    public DbSet<PaymentProof> PaymentProofs => Set<PaymentProof>();
    public DbSet<InstalmentPlan> InstalmentPlans => Set<InstalmentPlan>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<SchoolEvent> Events => Set<SchoolEvent>();
    public DbSet<GalleryPhoto> GalleryPhotos => Set<GalleryPhoto>();
    public DbSet<Testimonial> Testimonials => Set<Testimonial>();
    public DbSet<NoticeRead> NoticeReads => Set<NoticeRead>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<Newsletter> Newsletters => Set<Newsletter>();
    public DbSet<NewsletterOptOut> NewsletterOptOuts => Set<NewsletterOptOut>();
    public DbSet<SeedRun> SeedRuns => Set<SeedRun>();
    public DbSet<Domain.StaffAttendance.ClockSettings> ClockSettings => Set<Domain.StaffAttendance.ClockSettings>();
    public DbSet<Domain.StaffAttendance.StaffDay> StaffDays => Set<Domain.StaffAttendance.StaffDay>();

    public DbSet<AdmissionApplication> Applications => Set<AdmissionApplication>();
    public DbSet<AdmissionSettings> AdmissionSettings => Set<AdmissionSettings>();
    public DbSet<ExamSlot> ExamSlots => Set<ExamSlot>();
    public DbSet<Domain.Cbt.Question> Questions => Set<Domain.Cbt.Question>();
    public DbSet<Domain.Cbt.CbtTest> CbtTests => Set<Domain.Cbt.CbtTest>();
    public DbSet<Domain.Cbt.TestAttempt> TestAttempts => Set<Domain.Cbt.TestAttempt>();
    public DbSet<Domain.Cbt.AiGeneration> AiGenerations => Set<Domain.Cbt.AiGeneration>();
    public DbSet<Domain.Cbt.AiSettings> AiSettings => Set<Domain.Cbt.AiSettings>();
    public DbSet<Domain.Cbt.AiAllowance> AiAllowances => Set<Domain.Cbt.AiAllowance>();
    public DbSet<FormCode> FormCodes => Set<FormCode>();
    public DbSet<FormPayment> FormPayments => Set<FormPayment>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<NewsletterSubscriber> NewsletterSubscribers => Set<NewsletterSubscriber>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Entities create their own v7 ids. Telling EF the key is never database-generated makes a new child added
        // through a navigation (session.Terms.Add(term)) an INSERT; otherwise a set key reads as "already exists".
        foreach (var type in builder.Model.GetEntityTypes().Where(t => typeof(Domain.Common.Entity).IsAssignableFrom(t.ClrType)))
            builder.Entity(type.ClrType).Property(nameof(Domain.Common.Entity.Id)).ValueGeneratedNever();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(12, 2);
        configurationBuilder.Properties<string>().HaveMaxLength(256);
    }
}
