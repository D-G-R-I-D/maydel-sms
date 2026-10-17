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

    public string ChildName => $"{ChildFirstName} {ChildLastName}";

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

/// <summary>Admissions for one session (E11.4): open or closed, the form fee and the exam window.</summary>
public class AdmissionSettings : Entity
{
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
