using Maydel.Domain.Attendance;
using Maydel.Domain.Common;
using Maydel.Domain.People;

namespace Maydel.Domain.Admissions;

/// <summary>Where an application is in the pipeline (E11). Later stages arrive with the exam and offers.</summary>
public enum ApplicationStatus
{
    /// <summary>Applied; the admission form hasn't been paid for yet.</summary>
    AwaitingPayment,
    /// <summary>Form paid: the family can fill the form and book the entrance exam inside the window.</summary>
    FormPaid,
    Submitted,
    ExamBooked,
    /// <summary>The exam window closed without the exam being taken; the school can reopen it.</summary>
    Missed,
    Assessed,
    Offered,
    NotOffered,
    Accepted,
    Enrolled,
    Withdrawn,
}

public enum FormPaymentMethod
{
    Online,
    /// <summary>Paid at the school; the bursar issued a one-time form code.</summary>
    SchoolCode,
}

/// <summary>
/// An application for admission (E11.4+). Started by a family's applicant account (the same login later becomes their
/// parent account), for one child, one session and one class level.
/// </summary>
public class AdmissionApplication : Entity
{
    public required string ReferenceNumber { get; set; }
    public Guid SessionId { get; set; }
    public Guid ClassLevelId { get; set; }
    public Guid ApplicantUserId { get; set; }

    public required string ParentName { get; set; }
    public required string Phone { get; set; }
    public required string Email { get; set; }
    public required string ChildFirstName { get; set; }
    public required string ChildLastName { get; set; }
    public DateOnly? ChildDateOfBirth { get; set; }

    public ApplicationStatus Status { get; set; } = ApplicationStatus.AwaitingPayment;
    public FormPaymentMethod? FormPaymentMethod { get; set; }
    public decimal FormFee { get; set; }
    public DateTimeOffset? FormPaidAt { get; set; }
    public string? FormCode { get; set; }

    /// <summary>The last day the entrance exam can be booked or taken.</summary>
    public DateOnly? ExamWindowEndsOn { get; set; }
    public string? StaffNotes { get; set; }

    /// <summary>Set when the application is converted into an enrolled student.</summary>
    public Guid? StudentId { get; set; }

    /// <summary>The application form the family fills after paying (E11.5).</summary>
    public ApplicationForm Form { get; set; } = new();
    public DateTimeOffset? SubmittedAt { get; set; }
    public List<ApplicationDocument> Documents { get; set; } = [];

    // ---- Entrance exam (E11.6–7) ----
    public Guid? ExamSlotId { get; set; }
    public DateTimeOffset? ExamBookedAt { get; set; }
    public DateTimeOffset? ExamReminderSentAt { get; set; }
    public DateTimeOffset? MissedAt { get; set; }

    /// <summary>The entrance exam score as a percentage (0–100).</summary>
    public decimal? ExamScore { get; set; }
    public DateTimeOffset? ScoredAt { get; set; }

    /// <summary>When the "you passed the first stage" email was queued; each family is told once.</summary>
    public DateTimeOffset? StageOneNotifiedAt { get; set; }

    public string ChildName => $"{ChildFirstName} {ChildLastName}";

    /// <summary>
    /// Books (or moves) the entrance exam into <paramref name="slot"/>: after submitting the form, inside the exam window, before
    /// the slot starts and while it has seats. A booking can be moved until its own slot starts.
    /// </summary>
    public DomainError? BookExam(ExamSlot slot, int seatsTaken, DateTimeOffset? currentSlotStartsAt, DateTimeOffset now)
    {
        switch (Status)
        {
            case ApplicationStatus.FormPaid:
                return new("admission.submit_first", "Submit the admission form first, then book the exam.");
            case ApplicationStatus.Missed:
                return new("admission.missed", "The exam window has closed. Contact the school to reopen it.");
            case ApplicationStatus.ExamBooked when currentSlotStartsAt <= now:
                return new("admission.exam_started", "Your exam has already started or taken place, so the booking can't be changed.");
            case ApplicationStatus.Submitted or ApplicationStatus.ExamBooked:
                break;
            default:
                return new("admission.exam_closed", "The entrance exam can't be booked at this stage.");
        }
        if (slot.SessionId != SessionId) return new("admission.slot_not_found", "That exam time isn't available.", "slotId");
        if (slot.StartsAt <= now) return new("admission.slot_past", "That exam time has already passed. Choose another.", "slotId");
        if (ExamWindowEndsOn is { } last && slot.Day > last)
            return new("admission.slot_outside_window", $"Choose an exam time on or before {last:d MMMM yyyy}.", "slotId");
        if (ExamSlotId != slot.Id && seatsTaken >= slot.Capacity) return new("admission.slot_full", "That exam time is full. Choose another.", "slotId");
        (ExamSlotId, ExamBookedAt, ExamReminderSentAt, Status) = (slot.Id, now, null, ApplicationStatus.ExamBooked);
        return null;
    }

    /// <summary>Staff enter the score (paper exam or CBT total) as a percentage; it can be corrected until offers are made.</summary>
    public DomainError? RecordScore(decimal score, DateTimeOffset at)
    {
        if (Status is not (ApplicationStatus.ExamBooked or ApplicationStatus.Assessed))
            return new("admission.not_booked", "Scores can only be entered for applicants booked for the exam.");
        if (score is < 0 or > 100 || decimal.Round(score, 2) != score)
            return new("admission.score", "Enter a score from 0 to 100 (at most 2 decimal places).", "score");
        (ExamScore, ScoredAt, Status) = (score, at, ApplicationStatus.Assessed);
        return null;
    }

    /// <summary>The applicant didn't turn up: Missed (the school can reopen it).</summary>
    public DomainError? MarkAbsent(DateTimeOffset at)
    {
        if (Status != ApplicationStatus.ExamBooked) return new("admission.not_booked", "Only applicants booked for the exam can be marked absent.");
        (Status, MissedAt) = (ApplicationStatus.Missed, at);
        return null;
    }

    /// <summary>The window closed without a booking (form paid or submitted): Missed. Returns whether it changed.</summary>
    public bool ExpireIfOverdue(DateTimeOffset now)
    {
        if (Status is not (ApplicationStatus.FormPaid or ApplicationStatus.Submitted) || ExamWindowEndsOn is not { } last) return false;
        if (RegisterRules.TodayAt(now) <= last) return false;
        (Status, MissedAt) = (ApplicationStatus.Missed, now);
        return true;
    }

    /// <summary>The school gives a missed applicant a new window from today; the old booking and score are cleared.</summary>
    public DomainError? Reopen(DateOnly today, int windowDays)
    {
        if (Status != ApplicationStatus.Missed) return new("admission.not_missed", "Only applications that missed the exam can be reopened.");
        Status = SubmittedAt is null ? ApplicationStatus.FormPaid : ApplicationStatus.Submitted;
        (ExamWindowEndsOn, ExamSlotId, ExamBookedAt, ExamReminderSentAt, MissedAt) = (today.AddDays(windowDays), null, null, null, null);
        return null;
    }

    private static readonly DomainError FormLocked = new("admission.form_locked",
        "The form can only be changed after paying for it and before submitting it.");

    /// <summary>Saves the form as a draft (any fields may be blank until submission).</summary>
    public DomainError? SaveForm(ApplicationForm form, string childFirstName, string childLastName, DateOnly? dateOfBirth)
    {
        if (Status != ApplicationStatus.FormPaid) return FormLocked;
        if (string.IsNullOrWhiteSpace(childFirstName) || string.IsNullOrWhiteSpace(childLastName))
            return new("admission.child_name", "Enter the child's first and last name.", "childFirstName");
        (Form, ChildFirstName, ChildLastName, ChildDateOfBirth) = (form.Cleaned(), childFirstName.Trim(), childLastName.Trim(), dateOfBirth);
        return null;
    }

    /// <summary>
    /// An admin corrects a submitted form (E11.5 AC). Allowed from submission until the child is enrolled; the form must stay complete.
    /// Changes are audited automatically.
    /// </summary>
    public DomainError? CorrectForm(ApplicationForm form, string childFirstName, string childLastName, DateOnly? dateOfBirth)
    {
        if (Status is ApplicationStatus.AwaitingPayment or ApplicationStatus.FormPaid)
            return new("admission.not_submitted", "This form hasn't been submitted yet. The family can still change it themselves.");
        if (Status is ApplicationStatus.Enrolled or ApplicationStatus.Withdrawn)
            return new("admission.closed_application", "This application is closed. Change the student's record instead.");
        if (string.IsNullOrWhiteSpace(childFirstName) || string.IsNullOrWhiteSpace(childLastName))
            return new("admission.child_name", "Enter the child's first and last name.", "childFirstName");
        if (dateOfBirth is null) return new("admission.incomplete", "Enter the child's date of birth.", "childDateOfBirth");
        var cleaned = form.Cleaned();
        if (cleaned.Missing() is { } missing) return missing;
        (Form, ChildFirstName, ChildLastName, ChildDateOfBirth) = (cleaned, childFirstName.Trim(), childLastName.Trim(), dateOfBirth);
        return null;
    }

    /// <summary>Unlocks the form and opens the exam window (<paramref name="windowDays"/> days from <paramref name="today"/>).</summary>
    public DomainError? MarkFormPaid(FormPaymentMethod method, string? code, DateTimeOffset at, DateOnly today, int windowDays)
    {
        if (Status != ApplicationStatus.AwaitingPayment) return new("admission.already_paid", "The admission form for this application is already paid for.");
        (Status, FormPaymentMethod, FormCode, FormPaidAt, ExamWindowEndsOn) = (ApplicationStatus.FormPaid, method, code, at, today.AddDays(windowDays));
        return null;
    }

    public DomainError? CanChangeDocuments() => Status == ApplicationStatus.FormPaid ? null : FormLocked;

    /// <summary>Submits the form: everything the school needs must be there (see <see cref="ApplicationForm.Missing"/>).</summary>
    public DomainError? Submit(DateTimeOffset at)
    {
        if (Status != ApplicationStatus.FormPaid) return FormLocked;
        if (ChildDateOfBirth is null) return new("admission.incomplete", "Enter the child's date of birth.", "childDateOfBirth");
        if (Form.Missing() is { } missing) return missing;
        if (Documents.All(d => d.Kind != DocumentKind.PassportPhoto))
            return new("admission.incomplete", "Upload a recent passport photograph of the child.", "documents");
        (Status, SubmittedAt) = (ApplicationStatus.Submitted, at);
        return null;
    }
}

/// <summary>The fields of the admission form (E11.5). Stored with the application; nullable until submission.</summary>
public class ApplicationForm
{
    public string? ChildMiddleName { get; set; }
    public Sex? ChildSex { get; set; }
    public string? StateOfOrigin { get; set; }
    public string? Lga { get; set; }
    public string? Religion { get; set; }
    public string? HomeAddress { get; set; }
    public string? PreviousSchool { get; set; }
    public string? PreviousClass { get; set; }
    public string? MedicalNotes { get; set; }
    public string? SpecialNeeds { get; set; }
    public string? FatherName { get; set; }
    public string? FatherPhone { get; set; }
    public string? FatherEmail { get; set; }
    public string? FatherOccupation { get; set; }
    public string? MotherName { get; set; }
    public string? MotherPhone { get; set; }
    public string? MotherEmail { get; set; }
    public string? MotherOccupation { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }

    /// <summary>"The information given is true…": required to submit.</summary>
    public bool Declaration { get; set; }

    /// <summary>The first thing still needed before the form can be submitted, as a field error; null when complete.</summary>
    public DomainError? Missing()
    {
        if (ChildSex is null) return new("admission.incomplete", "Choose the child's sex.", "childSex");
        if (string.IsNullOrWhiteSpace(HomeAddress)) return new("admission.incomplete", "Enter the home address.", "homeAddress");
        var father = !string.IsNullOrWhiteSpace(FatherName) && !string.IsNullOrWhiteSpace(FatherPhone);
        var mother = !string.IsNullOrWhiteSpace(MotherName) && !string.IsNullOrWhiteSpace(MotherPhone);
        if (!father && !mother) return new("admission.incomplete", "Give at least one parent's name and phone number.", "fatherName");
        if (!Declaration) return new("admission.incomplete", "Tick the declaration to confirm the information is correct.", "declaration");
        return null;
    }

    public ApplicationForm Cleaned()
    {
        static string? C(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        return new ApplicationForm
        {
            ChildMiddleName = C(ChildMiddleName), ChildSex = ChildSex, StateOfOrigin = C(StateOfOrigin), Lga = C(Lga), Religion = C(Religion),
            HomeAddress = C(HomeAddress), PreviousSchool = C(PreviousSchool), PreviousClass = C(PreviousClass), MedicalNotes = C(MedicalNotes),
            SpecialNeeds = C(SpecialNeeds), FatherName = C(FatherName), FatherPhone = C(FatherPhone), FatherEmail = C(FatherEmail),
            FatherOccupation = C(FatherOccupation), MotherName = C(MotherName), MotherPhone = C(MotherPhone), MotherEmail = C(MotherEmail),
            MotherOccupation = C(MotherOccupation), EmergencyContactName = C(EmergencyContactName), EmergencyContactPhone = C(EmergencyContactPhone),
            Declaration = Declaration,
        };
    }
}

public enum DocumentKind
{
    PassportPhoto,
    BirthCertificate,
    LastReportCard,
    Other,
}

/// <summary>A private upload attached to an application (photo re-encoded; PDF signature-checked).</summary>
public class ApplicationDocument
{
    public const int MaxPerApplication = 6;

    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ApplicationId { get; set; }
    public DocumentKind Kind { get; set; }
    public required string FilePath { get; set; }
    public required string ContentType { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
}

public enum ExamMode
{
    Paper,
    /// <summary>Computer-based, at the school (E16).</summary>
    Cbt,
}

/// <summary>A sitting of the entrance exam the school opens for booking (E11.6).</summary>
public class ExamSlot : Entity
{
    public Guid SessionId { get; set; }

    /// <summary>Stored in UTC; shown in Abuja time.</summary>
    public DateTimeOffset StartsAt { get; set; }
    public int DurationMinutes { get; set; } = 60;
    public required string Venue { get; set; }
    public ExamMode Mode { get; set; } = ExamMode.Paper;
    public int Capacity { get; set; }
    public string? Notes { get; set; }

    /// <summary>Bumped on every booking so two families can't take the last seat at once (optimistic concurrency).</summary>
    public int Version { get; set; }

    public DateTimeOffset EndsAt => StartsAt.AddMinutes(DurationMinutes);

    /// <summary>The calendar day of the sitting in Abuja.</summary>
    public DateOnly Day => RegisterRules.TodayAt(StartsAt);

    public static DomainError? Check(DateTimeOffset startsAt, int durationMinutes, int capacity, DateTimeOffset now)
    {
        if (startsAt <= now) return new("exam_slot.past", "Choose a time in the future.", "startsAt");
        if (durationMinutes is < 15 or > 480) return new("exam_slot.duration", "The exam must last between 15 minutes and 8 hours.", "durationMinutes");
        if (capacity is < 1 or > 1000) return new("exam_slot.capacity", "Seats must be between 1 and 1000.", "capacity");
        return null;
    }
}

/// <summary>Admissions for one session (E11.4): open or closed, the form fee and the exam window.</summary>
public class AdmissionSettings : Entity
{
    /// <summary>The pass mark for the entrance exam (percentage); null until the admin sets it (E11.7).</summary>
    public decimal? CutOffMark { get; set; }

    public Guid SessionId { get; set; }
    public bool IsOpen { get; set; }
    public decimal FormFee { get; set; }

    /// <summary>Days after paying for the form in which the entrance exam must be booked and taken (default 5).</summary>
    public int ExamWindowDays { get; set; } = 5;
    public string? Instructions { get; set; }

    public static DomainError? Check(decimal formFee, int windowDays)
    {
        if (formFee < 0 || formFee > 10_000_000 || decimal.Round(formFee, 2) != formFee)
            return new("admission.fee", "The form fee must be ₦0 or more, with at most 2 decimal places.", "formFee");
        if (windowDays is < 1 or > 30) return new("admission.window", "The exam window must be between 1 and 30 days.", "examWindowDays");
        return null;
    }
}

/// <summary>A one-time code the bursar issues when a family pays for the form at the school (E11.4).</summary>
public class FormCode : Entity
{
    /// <summary>No 0/O or 1/I/L, so codes read out over the phone are unambiguous.</summary>
    public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public required string Code { get; set; }
    public Guid SessionId { get; set; }
    public decimal AmountPaid { get; set; }
    public string? IssuedTo { get; set; }
    public Guid? IssuedByUserId { get; set; }
    public Guid? UsedByApplicationId { get; set; }
    public DateTimeOffset? UsedAt { get; set; }

    public static string Generate(Func<int, int> random)
    {
        var chars = Enumerable.Range(0, 8).Select(_ => Alphabet[random(Alphabet.Length)]).ToArray();
        return $"{new string(chars, 0, 4)}-{new string(chars, 4, 4)}";
    }

    /// <summary>"abcd efgh" or "ABCD-EFGH" → "ABCD-EFGH"; null when it can't be a code.</summary>
    public static string? Normalise(string? input)
    {
        var raw = new string((input ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        return raw.Length == 8 && raw.All(c => Alphabet.Contains(c)) ? $"{raw[..4]}-{raw[4..]}" : null;
    }
}

public enum FormPaymentStatus
{
    Pending,
    Succeeded,
    Failed,
    NeedsReview,
}

/// <summary>An online payment for an admission form (E11.4), confirmed exactly like fee payments.</summary>
public class FormPayment : Entity
{
    public required string Reference { get; set; }
    public Guid ApplicationId { get; set; }
    public decimal Amount { get; set; }
    public FormPaymentStatus Status { get; set; } = FormPaymentStatus.Pending;
    public string? Message { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public enum MessageStatus
{
    New,
    Read,
    Replied,
    Archived,
}

public class ContactMessage : Entity
{
    public required string Name { get; set; }
    public required string Email { get; set; }
    public string? Subject { get; set; }
    public required string Message { get; set; }
    public MessageStatus Status { get; set; } = MessageStatus.New;
}

public class NewsletterSubscriber : Entity
{
    public required string Email { get; set; }
    public bool IsActive { get; set; } = true;
}
