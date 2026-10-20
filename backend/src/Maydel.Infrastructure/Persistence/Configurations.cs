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
        b.Property(x => x.WhatsApp).HasMaxLength(40);
        b.Property(x => x.MapUrl).HasMaxLength(500);
        b.Property(x => x.FacebookUrl).HasMaxLength(300);
        b.Property(x => x.InstagramUrl).HasMaxLength(300);
        b.Property(x => x.OfficeHours).HasMaxLength(120);
        b.Property(x => x.PrincipalName).HasMaxLength(80);
        b.Property(x => x.LogoPath).HasMaxLength(200);
        b.Property(x => x.SignaturePath).HasMaxLength(200);
    }
}

internal sealed class OutboxEmailConfig : IEntityTypeConfiguration<Domain.Communication.OutboxEmail>
{
    public void Configure(EntityTypeBuilder<Domain.Communication.OutboxEmail> b)
    {
        b.ToTable("OutboxEmails");
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.ToAddress).HasMaxLength(254);
        b.Property(x => x.ToName).HasMaxLength(120);
        b.Property(x => x.Subject).HasMaxLength(200);
        b.Property(x => x.Body).HasMaxLength(4000);
        b.Property(x => x.LastError).HasMaxLength(500);
        b.HasIndex(x => new { x.Status, x.NextAttemptAt });
        b.HasIndex(x => new { x.StudentId, x.TermId });
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
        b.Property(x => x.Name).HasMaxLength(80);
        b.Property(x => x.Amount).HasPrecision(14, 2);
        b.Property(x => x.Audience).HasConversion<string>().HasMaxLength(16);
        b.HasIndex(x => x.TermId);
        b.HasOne(x => x.Term).WithMany().HasForeignKey(x => x.TermId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.ClassLevel).WithMany().HasForeignKey(x => x.ClassLevelId).OnDelete(DeleteBehavior.Cascade);
        b.OwnsMany(x => x.OptIns, o =>
        {
            o.ToTable("FeeOptIns");
            o.WithOwner().HasForeignKey(x => x.FeeItemId);
            o.HasKey(x => new { x.FeeItemId, x.StudentId });
            o.HasOne<Domain.People.Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

internal sealed class PaymentProofConfig : IEntityTypeConfiguration<PaymentProof>
{
    public void Configure(EntityTypeBuilder<PaymentProof> b)
    {
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        b.HasIndex(x => x.InvoiceId);
        b.Property(x => x.Amount).HasPrecision(14, 2);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.BankReference).HasMaxLength(100);
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.RejectReason).HasMaxLength(300);
        b.Property(x => x.FilePath).HasMaxLength(200);
        b.Property(x => x.ContentType).HasMaxLength(60);
        b.HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OnlinePaymentConfig : IEntityTypeConfiguration<OnlinePayment>
{
    public void Configure(EntityTypeBuilder<OnlinePayment> b)
    {
        b.HasIndex(x => x.Reference).IsUnique();
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        b.Property(x => x.Reference).HasMaxLength(100);
        b.Property(x => x.Amount).HasPrecision(14, 2);
        b.Property(x => x.Excess).HasPrecision(14, 2);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Message).HasMaxLength(300);
        b.HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FeeSettingsConfig : IEntityTypeConfiguration<FeeSettings>
{
    public void Configure(EntityTypeBuilder<FeeSettings> b)
    {
        b.ToTable("FeeSettings");
        b.Property(x => x.SiblingMode).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.SecondChild).HasPrecision(14, 2);
        b.Property(x => x.ThirdAndLater).HasPrecision(14, 2);
    }
}

internal sealed class InstalmentPlanConfig : IEntityTypeConfiguration<InstalmentPlan>
{
    public void Configure(EntityTypeBuilder<InstalmentPlan> b)
    {
        b.HasIndex(x => x.TermId).IsUnique().HasFilter("\"TermId\" IS NOT NULL");
        b.HasIndex(x => x.SessionId).IsUnique().HasFilter("\"SessionId\" IS NOT NULL");
        b.HasOne<Term>().WithMany().HasForeignKey(x => x.TermId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<AcademicSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        b.ToTable(t => t.HasCheckConstraint("CK_InstalmentPlans_Scope", "(\"TermId\" IS NULL) <> (\"SessionId\" IS NULL)"));
        b.OwnsMany(x => x.Instalments, i =>
        {
            i.ToTable("Instalments");
            i.WithOwner().HasForeignKey(x => x.PlanId);
            i.HasKey(x => new { x.PlanId, x.Order });
            i.Property(x => x.Percent).HasPrecision(5, 2);
        });
    }
}

internal sealed class InvoiceConfig : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> b)
    {
        b.HasIndex(x => x.Number).IsUnique();
        // One live invoice per student per term (E10.2): generating again never duplicates.
        b.HasIndex(x => new { x.StudentId, x.TermId }).IsUnique().HasFilter("\"IsCancelled\" = false");
        b.HasIndex(x => new { x.TermId, x.ClassRoomId });
        b.Property(x => x.Number).HasMaxLength(40);
        b.Property(x => x.CancelReason).HasMaxLength(300);
        b.Ignore(x => x.IsPublished);
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
            l.Property(x => x.Description).HasMaxLength(120);
            l.Property(x => x.Amount).HasPrecision(14, 2);
        });
        b.HasMany(x => x.Payments).WithOne(p => p.Invoice).HasForeignKey(p => p.InvoiceId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PaymentConfig : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.HasIndex(x => x.ReceiptNumber).IsUnique();
        b.Property(x => x.ReceiptNumber).HasMaxLength(40);
        b.Property(x => x.Method).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Amount).HasPrecision(14, 2);
        b.Property(x => x.Reference).HasMaxLength(100);
        b.Property(x => x.Note).HasMaxLength(300);
        b.Ignore(x => x.IsReversal);
        // At most one reversal per payment, enforced by the database too.
        b.HasIndex(x => x.ReversesPaymentId).IsUnique().HasFilter("\"ReversesPaymentId\" IS NOT NULL");
        b.HasOne<Payment>().WithMany().HasForeignKey(x => x.ReversesPaymentId).OnDelete(DeleteBehavior.Restrict);
        // A gateway reference is recorded once, even if the webhook and the parent's return arrive together.
        b.HasIndex(x => x.Reference).IsUnique().HasFilter("\"Method\" = 'Online' AND \"ReversesPaymentId\" IS NULL");
    }
}

internal sealed class AnnouncementConfig : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> b)
    {
        b.Property(x => x.Body).HasMaxLength(20000);
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.Slug).HasMaxLength(100);
        b.Property(x => x.Summary).HasMaxLength(500);
        b.Property(x => x.Category).HasMaxLength(60);
        b.Property(x => x.ImagePath).HasMaxLength(200);
        b.Property(x => x.LinkUrl).HasMaxLength(500);
        b.Property(x => x.LinkText).HasMaxLength(60);
        b.Property(x => x.Audience).HasConversion<int>();
        b.Ignore(x => x.IsPublic);
        b.HasIndex(x => x.PublishedAt);
        b.HasIndex(x => x.Slug).IsUnique().HasFilter("\"Slug\" <> ''");
    }
}

internal sealed class EventConfig : IEntityTypeConfiguration<SchoolEvent>
{
    public void Configure(EntityTypeBuilder<SchoolEvent> b)
    {
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.Location).HasMaxLength(200);
        b.HasIndex(x => x.StartsAt);
    }
}

internal sealed class GalleryPhotoConfig : IEntityTypeConfiguration<GalleryPhoto>
{
    public void Configure(EntityTypeBuilder<GalleryPhoto> b)
    {
        b.Property(x => x.Caption).HasMaxLength(1000);
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.Category).HasMaxLength(60);
        b.Property(x => x.ImagePath).HasMaxLength(200);
    }
}

internal sealed class ApplicationConfig : IEntityTypeConfiguration<AdmissionApplication>
{
    public void Configure(EntityTypeBuilder<AdmissionApplication> b)
    {
        b.HasIndex(x => x.ReferenceNumber).IsUnique();
        b.HasIndex(x => new { x.SessionId, x.Status });
        b.HasIndex(x => x.ApplicantUserId);
        b.Property(x => x.ReferenceNumber).HasMaxLength(30);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.FormPaymentMethod).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.ParentName).HasMaxLength(120);
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.Email).HasMaxLength(254);
        b.Property(x => x.ChildFirstName).HasMaxLength(60);
        b.Property(x => x.ChildLastName).HasMaxLength(60);
        b.Property(x => x.FormFee).HasPrecision(14, 2);
        b.Property(x => x.FormCode).HasMaxLength(9);
        b.Property(x => x.StaffNotes).HasMaxLength(4000);
        b.Ignore(x => x.ChildName);
        b.Property(x => x.ExamScore).HasPrecision(5, 2);
        b.Property(x => x.Decision).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.AdmissionNumber).HasMaxLength(20);
        b.HasIndex(x => x.AdmissionNumber).IsUnique().HasFilter("\"AdmissionNumber\" IS NOT NULL");
        b.HasOne<ExamSlot>().WithMany().HasForeignKey(x => x.ExamSlotId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AcademicSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ClassLevel>().WithMany().HasForeignKey(x => x.ClassLevelId).OnDelete(DeleteBehavior.Restrict);
        b.OwnsOne(x => x.Form, f =>
        {
            f.Property(x => x.ChildSex).HasConversion<string>().HasMaxLength(8);
            foreach (var name in new[] { nameof(ApplicationForm.ChildMiddleName), nameof(ApplicationForm.StateOfOrigin), nameof(ApplicationForm.Lga),
                nameof(ApplicationForm.Religion), nameof(ApplicationForm.PreviousSchool), nameof(ApplicationForm.PreviousClass),
                nameof(ApplicationForm.FatherName), nameof(ApplicationForm.FatherOccupation), nameof(ApplicationForm.MotherName),
                nameof(ApplicationForm.MotherOccupation), nameof(ApplicationForm.EmergencyContactName) })
                f.Property(name).HasMaxLength(120);
            foreach (var name in new[] { nameof(ApplicationForm.FatherPhone), nameof(ApplicationForm.MotherPhone), nameof(ApplicationForm.EmergencyContactPhone) })
                f.Property(name).HasMaxLength(30);
            f.Property(x => x.FatherEmail).HasMaxLength(254);
            f.Property(x => x.MotherEmail).HasMaxLength(254);
            f.Property(x => x.HomeAddress).HasMaxLength(300);
            f.Property(x => x.MedicalNotes).HasMaxLength(1000);
            f.Property(x => x.SpecialNeeds).HasMaxLength(1000);
        });
        b.Navigation(x => x.Form).IsRequired();
        b.OwnsMany(x => x.Documents, d =>
        {
            d.ToTable("ApplicationDocuments");
            d.WithOwner().HasForeignKey(x => x.ApplicationId);
            d.HasKey(x => x.Id);
            d.Property(x => x.Id).ValueGeneratedNever();
            d.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            d.Property(x => x.FilePath).HasMaxLength(200);
            d.Property(x => x.ContentType).HasMaxLength(60);
        });
    }
}

internal sealed class QuestionConfig : IEntityTypeConfiguration<Domain.Cbt.Question>
{
    public void Configure(EntityTypeBuilder<Domain.Cbt.Question> b)
    {
        b.HasIndex(x => new { x.SubjectId, x.ClassLevelId, x.IsArchived });
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Difficulty).HasConversion<string>().HasMaxLength(8);
        b.Property(x => x.Source).HasConversion<string>().HasMaxLength(8);
        b.Property(x => x.Text).HasMaxLength(Domain.Cbt.Question.MaxTextLength);
        b.Property(x => x.MarkingGuide).HasMaxLength(Domain.Cbt.Question.MaxTextLength);
        b.Property(x => x.Topic).HasMaxLength(80);
        b.Property(x => x.ImagePath).HasMaxLength(200);
        b.Ignore(x => x.IsAutoMarked);
        b.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ClassLevel>().WithMany().HasForeignKey(x => x.ClassLevelId).OnDelete(DeleteBehavior.Restrict);
        b.OwnsMany(x => x.Options, o =>
        {
            o.ToTable("QuestionOptions");
            o.WithOwner().HasForeignKey("QuestionId");
            o.HasKey(x => x.Id);
            o.Property(x => x.Id).ValueGeneratedNever();
            o.Property(x => x.Text).HasMaxLength(1000);
        });
    }
}

internal sealed class CbtTestConfig : IEntityTypeConfiguration<Domain.Cbt.CbtTest>
{
    public void Configure(EntityTypeBuilder<Domain.Cbt.CbtTest> b)
    {
        b.ToTable("CbtTests");
        b.HasIndex(x => new { x.TermId, x.SubjectId, x.ClassLevelId });
        b.Property(x => x.Title).HasMaxLength(150);
        b.Property(x => x.Instructions).HasMaxLength(2000);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(10);
        b.Ignore(x => x.QuestionCount);
        b.Ignore(x => x.TotalMarks);
        b.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ClassLevel>().WithMany().HasForeignKey(x => x.ClassLevelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Term>().WithMany().HasForeignKey(x => x.TermId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Domain.Assessment.AssessmentComponent>().WithMany().HasForeignKey(x => x.ComponentId).OnDelete(DeleteBehavior.Restrict);
        b.OwnsMany(x => x.Sections, s =>
        {
            s.ToTable("CbtTestSections");
            s.WithOwner().HasForeignKey("TestId");
            s.HasKey(x => x.Id);
            s.Property(x => x.Id).ValueGeneratedNever();
            s.Property(x => x.Title).HasMaxLength(100);
            s.Property(x => x.Instructions).HasMaxLength(1000);
            s.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            s.Property(x => x.Mode).HasConversion<string>().HasMaxLength(8);
            s.Property(x => x.Difficulty).HasConversion<string>().HasMaxLength(8);
            s.Property(x => x.Topic).HasMaxLength(80);
            s.Property(x => x.MarksPerQuestion).HasPrecision(6, 2);
            s.Ignore(x => x.QuestionCount);
            s.Ignore(x => x.Marks);
        });
    }
}

internal sealed class TestAttemptConfig : IEntityTypeConfiguration<Domain.Cbt.TestAttempt>
{
    public void Configure(EntityTypeBuilder<Domain.Cbt.TestAttempt> b)
    {
        b.ToTable("TestAttempts");
        // One sitting number per student per test: guards against a double "start" creating two attempts.
        b.HasIndex(x => new { x.TestId, x.StudentId, x.Number }).IsUnique();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(12);
        b.Property(x => x.SessionToken).HasMaxLength(64);
        b.Ignore(x => x.IsFullyMarked);
        b.Ignore(x => x.TotalScore);
        b.Ignore(x => x.NeedsMarking);
        b.HasOne<Domain.Cbt.CbtTest>().WithMany().HasForeignKey(x => x.TestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        b.OwnsMany(x => x.Items, i =>
        {
            i.ToTable("TestAttemptItems");
            i.WithOwner().HasForeignKey("AttemptId");
            i.HasKey(x => x.Id);
            i.Property(x => x.Id).ValueGeneratedNever();
            i.Property(x => x.Marks).HasPrecision(6, 2);
            i.Property(x => x.AutoScore).HasPrecision(6, 2);
            i.Property(x => x.TeacherScore).HasPrecision(6, 2);
            i.Property(x => x.TeacherComment).HasMaxLength(1000);
            i.Ignore(x => x.Score);
            i.Property(x => x.AnswerText).HasMaxLength(5000);
        });
        b.OwnsMany(x => x.Events, e =>
        {
            e.ToTable("TestAttemptEvents");
            e.WithOwner().HasForeignKey("AttemptId");
            e.Property<int>("Id").ValueGeneratedOnAdd();
            e.HasKey("Id");
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(10);
        });
    }
}

internal sealed class AiGenerationConfig : IEntityTypeConfiguration<Domain.Cbt.AiGeneration>
{
    public void Configure(EntityTypeBuilder<Domain.Cbt.AiGeneration> b)
    {
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.Property(x => x.AssessmentType).HasConversion<string>().HasMaxLength(8);
        b.Property(x => x.Guidance).HasMaxLength(4000);
        // Kinds as their names (e.g. {Objective,Theory}) so the column reads well and survives enum reordering.
        b.Property(x => x.Kinds).HasConversion(
            v => v.Select(k => k.ToString()).ToList(),
            v => v.Select(Enum.Parse<Domain.Cbt.QuestionKind>).ToList(),
            new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<Domain.Cbt.QuestionKind>>(
                (a, c) => a!.SequenceEqual(c!), v => v.Aggregate(0, (h, k) => HashCode.Combine(h, k)), v => v.ToList()));
        b.Ignore(x => x.Regenerations);
        b.Ignore(x => x.CanRegenerate);
        b.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ClassLevel>().WithMany().HasForeignKey(x => x.ClassLevelId).OnDelete(DeleteBehavior.Restrict);
        b.OwnsMany(x => x.Files, f =>
        {
            f.ToTable("AiSourceFiles");
            f.WithOwner().HasForeignKey("GenerationId");
            f.HasKey(x => x.Id);
            f.Property(x => x.Id).ValueGeneratedNever();
            f.Property(x => x.Name).HasMaxLength(200);
            f.Property(x => x.FilePath).HasMaxLength(200);
            f.Property(x => x.ContentType).HasMaxLength(60);
        });
        b.OwnsMany(x => x.Versions, v =>
        {
            v.ToTable("AiVersions");
            v.WithOwner().HasForeignKey("GenerationId");
            v.HasKey(x => x.Id);
            v.Property(x => x.Id).ValueGeneratedNever();
            v.Property(x => x.Status).HasConversion<string>().HasMaxLength(8);
            v.Property(x => x.Message).HasMaxLength(500);
            v.Property(x => x.Model).HasMaxLength(60);
            v.Property(x => x.QuestionsJson).HasColumnType("jsonb");
        });
    }
}

internal sealed class AiAllowanceConfig : IEntityTypeConfiguration<Domain.Cbt.AiAllowance>
{
    public void Configure(EntityTypeBuilder<Domain.Cbt.AiAllowance> b) => b.HasIndex(x => x.UserId).IsUnique();
}

internal sealed class ExamSlotConfig : IEntityTypeConfiguration<ExamSlot>
{
    public void Configure(EntityTypeBuilder<ExamSlot> b)
    {
        b.HasIndex(x => new { x.SessionId, x.StartsAt });
        b.Property(x => x.Venue).HasMaxLength(120);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Property(x => x.Mode).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.Ignore(x => x.EndsAt);
        b.Ignore(x => x.Day);
        b.HasOne<AcademicSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AdmissionSettingsConfig : IEntityTypeConfiguration<AdmissionSettings>
{
    public void Configure(EntityTypeBuilder<AdmissionSettings> b)
    {
        b.HasIndex(x => x.SessionId).IsUnique();
        b.Property(x => x.FormFee).HasPrecision(14, 2);
        b.Property(x => x.CutOffMark).HasPrecision(5, 2);
        b.HasOne<Term>().WithMany().HasForeignKey(x => x.EntryTermId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.Instructions).HasMaxLength(2000);
        b.HasOne<AcademicSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class FormCodeConfig : IEntityTypeConfiguration<FormCode>
{
    public void Configure(EntityTypeBuilder<FormCode> b)
    {
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.SessionId);
        b.Property(x => x.Code).HasMaxLength(9);
        b.Property(x => x.AmountPaid).HasPrecision(14, 2);
        b.Property(x => x.IssuedTo).HasMaxLength(120);
    }
}

internal sealed class FormPaymentConfig : IEntityTypeConfiguration<FormPayment>
{
    public void Configure(EntityTypeBuilder<FormPayment> b)
    {
        b.HasIndex(x => x.Reference).IsUnique();
        b.Property(x => x.Reference).HasMaxLength(100);
        b.Property(x => x.Amount).HasPrecision(14, 2);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Message).HasMaxLength(300);
        b.HasOne<AdmissionApplication>().WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ContactMessageConfig : IEntityTypeConfiguration<ContactMessage>
{
    public void Configure(EntityTypeBuilder<ContactMessage> b)
    {
        b.Property(x => x.Message).HasMaxLength(4000);
        b.Property(x => x.Name).HasMaxLength(120);
        b.Property(x => x.Email).HasMaxLength(254);
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.Subject).HasMaxLength(150);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
    }
}

internal sealed class NewsletterConfig : IEntityTypeConfiguration<NewsletterSubscriber>
{
    public void Configure(EntityTypeBuilder<NewsletterSubscriber> b)
    {
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.Email).HasMaxLength(254);
    }
}

internal sealed class TestimonialConfig : IEntityTypeConfiguration<Testimonial>
{
    public void Configure(EntityTypeBuilder<Testimonial> b)
    {
        b.Property(x => x.Quote).HasMaxLength(1000);
        b.Property(x => x.Attribution).HasMaxLength(120);
    }
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
