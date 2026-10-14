using Maydel.Domain.Academics;
using Maydel.Domain.Admissions;
using Maydel.Domain.Assessment;
using Maydel.Domain.Attendance;
using Maydel.Domain.Audit;
using Maydel.Domain.Communication;
using Maydel.Domain.Finance;
using Maydel.Domain.People;
using Maydel.Application.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maydel.Infrastructure.Persistence;

// Strings default to 256 chars (see AppDbContext.ConfigureConventions); long-text columns opt out here.

internal sealed class AppUserConfig : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b) => b.Property(x => x.FullName).HasMaxLength(200);
}

internal sealed class RefreshTokenConfig : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.FamilyId);
        b.Property(x => x.UserAgent).HasMaxLength(512);
        b.Property(x => x.RevokedReason).HasConversion<string>().HasMaxLength(32);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SessionConfig : IEntityTypeConfiguration<AcademicSession>
{
    public void Configure(EntityTypeBuilder<AcademicSession> b)
    {
        b.HasIndex(x => x.Name).IsUnique();
        b.HasMany(x => x.Terms).WithOne(t => t.Session).HasForeignKey(t => t.SessionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TermConfig : IEntityTypeConfiguration<Term>
{
    public void Configure(EntityTypeBuilder<Term> b)
    {
        b.HasIndex(x => new { x.SessionId, x.Number }).IsUnique();
        b.Ignore(x => x.DisplayName);
    }
}

internal sealed class ClassLevelConfig : IEntityTypeConfiguration<ClassLevel>
{
    public void Configure(EntityTypeBuilder<ClassLevel> b)
    {
        b.HasIndex(x => x.Name).IsUnique();
        b.Property(x => x.Section).HasConversion<string>().HasMaxLength(32);
        b.HasMany(x => x.ClassRooms).WithOne(c => c.ClassLevel).HasForeignKey(c => c.ClassLevelId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ClassRoomConfig : IEntityTypeConfiguration<ClassRoom>
{
    public void Configure(EntityTypeBuilder<ClassRoom> b)
    {
        b.HasIndex(x => new { x.ClassLevelId, x.Arm }).IsUnique();
        b.Ignore(x => x.DisplayName);
        b.HasOne(x => x.FormTeacher).WithMany().HasForeignKey(x => x.FormTeacherId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(x => x.Subjects).WithOne(s => s.ClassRoom).HasForeignKey(s => s.ClassRoomId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SubjectConfig : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> b)
    {
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.Name).IsUnique();
    }
}

internal sealed class ClassSubjectConfig : IEntityTypeConfiguration<ClassSubject>
{
    public void Configure(EntityTypeBuilder<ClassSubject> b)
    {
        b.HasIndex(x => new { x.ClassRoomId, x.SubjectId }).IsUnique();
        b.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class StudentConfig : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> b)
    {
        b.HasIndex(x => x.AdmissionNumber).IsUnique();
        b.HasIndex(x => new { x.LastName, x.FirstName });
        b.HasIndex(x => x.UserId).IsUnique();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Sex).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.HomeAddress).HasMaxLength(1000);
        b.Property(x => x.MedicalNotes).HasMaxLength(2000);
        b.Ignore(x => x.FullName);
        b.HasOne(x => x.ClassRoom).WithMany().HasForeignKey(x => x.ClassRoomId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class GuardianConfig : IEntityTypeConfiguration<Guardian>
{
    public void Configure(EntityTypeBuilder<Guardian> b)
    {
        // Phones are stored normalised (+234…); one guardian record per phone so siblings share a parent account.
        b.HasIndex(x => x.Phone).IsUnique();
        b.Property(x => x.Phone).HasMaxLength(20);
        b.HasIndex(x => x.Email);
        b.HasIndex(x => x.UserId).IsUnique();
        b.Property(x => x.Address).HasMaxLength(1000);
    }
}

internal sealed class StudentGuardianConfig : IEntityTypeConfiguration<StudentGuardian>
{
    public void Configure(EntityTypeBuilder<StudentGuardian> b)
    {
        b.HasKey(x => new { x.StudentId, x.GuardianId });
        b.Property(x => x.Relationship).HasConversion<string>().HasMaxLength(32);
        b.HasOne(x => x.Student).WithMany(s => s.Guardians).HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Guardian).WithMany(g => g.Students).HasForeignKey(x => x.GuardianId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class StaffConfig : IEntityTypeConfiguration<Staff>
{
    public void Configure(EntityTypeBuilder<Staff> b)
    {
        b.HasIndex(x => x.StaffNumber).IsUnique();
        b.HasIndex(x => x.Email).IsUnique();
        b.HasIndex(x => x.UserId).IsUnique();
        b.Property(x => x.Bio).HasMaxLength(2000);
    }
}

internal sealed class SubjectScoreConfig : IEntityTypeConfiguration<SubjectScore>
{
    public void Configure(EntityTypeBuilder<SubjectScore> b)
    {
        b.HasIndex(x => new { x.StudentId, x.TermId, x.SubjectId }).IsUnique();
        b.HasIndex(x => new { x.ClassRoomId, x.TermId, x.SubjectId });
        b.Property(x => x.TeacherComment).HasMaxLength(1000);
        b.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Term).WithMany().HasForeignKey(x => x.TermId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
        b.OwnsMany(x => x.Components, c =>
        {
            c.ToTable("ComponentScores");
            c.WithOwner().HasForeignKey(x => x.SubjectScoreId);
            c.HasKey(x => new { x.SubjectScoreId, x.ComponentId });
            c.HasOne(x => x.Component).WithMany().HasForeignKey(x => x.ComponentId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

internal sealed class TermReportConfig : IEntityTypeConfiguration<TermReport>
{
    public void Configure(EntityTypeBuilder<TermReport> b)
    {
        b.HasIndex(x => new { x.StudentId, x.TermId }).IsUnique();
        b.HasIndex(x => new { x.ClassRoomId, x.TermId });
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.FormTeacherComment).HasMaxLength(2000);
        b.Property(x => x.PrincipalComment).HasMaxLength(2000);
        b.Ignore(x => x.DaysAbsent);
        b.OwnsMany(x => x.TraitRatings, r =>
        {
            r.ToTable("TraitRatings");
            r.WithOwner().HasForeignKey(x => x.TermReportId);
            r.HasKey(x => new { x.TermReportId, x.TraitId });
            r.Property(x => x.Rating).HasMaxLength(2);
            r.HasOne(x => x.Trait).WithMany().HasForeignKey(x => x.TraitId).OnDelete(DeleteBehavior.Restrict);
        });
        b.OwnsMany(x => x.Clubs, c =>
        {
            c.ToTable("ReportClubs");
            c.WithOwner().HasForeignKey(x => x.TermReportId);
            c.HasKey(x => new { x.TermReportId, x.Order });
            c.Property(x => x.Name).HasMaxLength(40);
            c.Property(x => x.Rating).HasMaxLength(30);
        });
        b.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Term).WithMany().HasForeignKey(x => x.TermId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ClassRoom).WithMany().HasForeignKey(x => x.ClassRoomId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SchoolProfileConfig : IEntityTypeConfiguration<Domain.Settings.SchoolProfile>
{
    public void Configure(EntityTypeBuilder<Domain.Settings.SchoolProfile> b)
    {
        b.ToTable("SchoolProfile");
        b.Property(x => x.Name).HasMaxLength(120);
        b.Property(x => x.Motto).HasMaxLength(120);
        b.Property(x => x.Address).HasMaxLength(300);
        b.Property(x => x.Phone).HasMaxLength(40);
        b.Property(x => x.Email).HasMaxLength(120);
        b.Property(x => x.Website).HasMaxLength(120);
        b.Property(x => x.PrincipalName).HasMaxLength(80);
        b.Property(x => x.LogoPath).HasMaxLength(200);
        b.Property(x => x.SignaturePath).HasMaxLength(200);
    }
}

internal sealed class BehaviourTraitConfig : IEntityTypeConfiguration<BehaviourTrait>
{
    public void Configure(EntityTypeBuilder<BehaviourTrait> b) => b.Property(x => x.Name).HasMaxLength(60);
}

internal sealed class AttendanceConfig : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> b)
    {
        b.HasIndex(x => new { x.StudentId, x.Date }).IsUnique();
        b.HasIndex(x => new { x.ClassRoomId, x.Date });
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Ignore(x => x.CountsAsPresent);
        b.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class FeeItemConfig : IEntityTypeConfiguration<FeeItem>
{
    public void Configure(EntityTypeBuilder<FeeItem> b)
    {
        b.HasOne(x => x.Term).WithMany().HasForeignKey(x => x.TermId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.ClassLevel).WithMany().HasForeignKey(x => x.ClassLevelId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class InvoiceConfig : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> b)
    {
        b.HasIndex(x => x.Number).IsUnique();
        b.HasIndex(x => new { x.StudentId, x.TermId });
        b.Ignore(x => x.Total);
        b.Ignore(x => x.AmountPaid);
        b.Ignore(x => x.Balance);
        b.Ignore(x => x.Status);
        b.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Term).WithMany().HasForeignKey(x => x.TermId).OnDelete(DeleteBehavior.Restrict);
        b.OwnsMany(x => x.Lines, l =>
        {
            l.ToTable("InvoiceLines");
            l.WithOwner().HasForeignKey(x => x.InvoiceId);
            l.HasKey(x => x.Id);
            l.Property(x => x.Id).ValueGeneratedNever();
        });
        b.HasMany(x => x.Payments).WithOne(p => p.Invoice).HasForeignKey(p => p.InvoiceId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PaymentConfig : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.HasIndex(x => x.ReceiptNumber).IsUnique();
        b.Property(x => x.Method).HasConversion<string>().HasMaxLength(32);
    }
}

internal sealed class AnnouncementConfig : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> b)
    {
        b.Property(x => x.Body).HasMaxLength(20000);
        b.HasIndex(x => x.PublishedAt);
    }
}

internal sealed class EventConfig : IEntityTypeConfiguration<SchoolEvent>
{
    public void Configure(EntityTypeBuilder<SchoolEvent> b)
    {
        b.Property(x => x.Description).HasMaxLength(4000);
        b.HasIndex(x => x.StartsAt);
    }
}

internal sealed class GalleryPhotoConfig : IEntityTypeConfiguration<GalleryPhoto>
{
    public void Configure(EntityTypeBuilder<GalleryPhoto> b) => b.Property(x => x.Caption).HasMaxLength(1000);
}

internal sealed class ApplicationConfig : IEntityTypeConfiguration<AdmissionApplication>
{
    public void Configure(EntityTypeBuilder<AdmissionApplication> b)
    {
        b.HasIndex(x => x.ReferenceNumber).IsUnique();
        b.HasIndex(x => x.Status);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Message).HasMaxLength(4000);
        b.Property(x => x.StaffNotes).HasMaxLength(4000);
    }
}

internal sealed class ContactMessageConfig : IEntityTypeConfiguration<ContactMessage>
{
    public void Configure(EntityTypeBuilder<ContactMessage> b)
    {
        b.Property(x => x.Message).HasMaxLength(4000);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
    }
}

internal sealed class NewsletterConfig : IEntityTypeConfiguration<NewsletterSubscriber>
{
    public void Configure(EntityTypeBuilder<NewsletterSubscriber> b) => b.HasIndex(x => x.Email).IsUnique();
}

internal sealed class AuditLogConfig : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.HasIndex(x => x.Timestamp);
        b.HasIndex(x => new { x.EntityType, x.EntityId });
        b.Property(x => x.Changes).HasColumnType("jsonb");
    }
}
