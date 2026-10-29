using Maydel.Domain.Academics;
using Maydel.Domain.Common;

namespace Maydel.Domain.People;

public enum Sex
{
    Female,
    Male,
}

public enum StudentStatus
{
    Active,
    Graduated,
    Withdrawn,
    Suspended,
}

public enum GuardianRelationship
{
    Mother,
    Father,
    Guardian,
    Other,
}

public class Student : Entity
{
    /// <summary>Unique, human-facing ID (e.g. MSS/2026/0001). Also the student's login name.</summary>
    public required string AdmissionNumber { get; set; }
    public required string FirstName { get; set; }
    public string? MiddleName { get; set; }
    public required string LastName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public Sex? Sex { get; set; }
    public string? StateOfOrigin { get; set; }
    public string? Lga { get; set; }
    public string? HomeAddress { get; set; }
    public string? House { get; set; }
    public string? MedicalNotes { get; set; }
    public string? PhotoPath { get; set; }
    public DateOnly AdmittedOn { get; set; }
    public StudentStatus Status { get; set; } = StudentStatus.Active;

    public Guid? ClassRoomId { get; set; }
    public ClassRoom? ClassRoom { get; set; }

    /// <summary>Identity user for the student's own login, if one has been issued.</summary>
    public Guid? UserId { get; set; }

    public List<StudentGuardian> Guardians { get; set; } = [];

    public string FullName =>
        string.Join(' ', new[] { FirstName, MiddleName, LastName }.Where(p => !string.IsNullOrWhiteSpace(p)));

    public bool IsActive => Status == StudentStatus.Active;

    /// <summary>MSS/{year}/{seq:0000}, e.g. MSS/2026/0007. Sequence restarts each admission year.</summary>
    public static string AdmissionNumberFor(int year, int sequence) => $"MSS/{year}/{sequence:0000}";

    public static string AdmissionPrefix(int year) => $"MSS/{year}/";

    /// <summary>Date of birth can't be in the future or implausibly old for a pupil (1–25 years at admission).</summary>
    public static DomainError? ValidateDates(DateOnly? dateOfBirth, DateOnly admittedOn, DateOnly today)
    {
        if (admittedOn > today.AddYears(1)) return new("student.admitted_future", "The admission date is too far in the future.", "admittedOn");
        if (dateOfBirth is not { } dob) return null;
        if (dob > today) return new("student.dob_future", "Date of birth can't be in the future.", "dateOfBirth");
        if (dob > admittedOn.AddYears(-1) || dob < admittedOn.AddYears(-25))
            return new("student.dob_range", "Check the date of birth: pupils are 1 to 25 years old at admission.", "dateOfBirth");
        return null;
    }

    /// <summary>
    /// Active ⇄ Suspended; Active or Suspended → Withdrawn or Graduated; Withdrawn → Active (re-admission).
    /// Graduated is final. Non-active students lose portal access; their records are kept.
    /// </summary>
    public DomainError? ChangeStatus(StudentStatus to)
    {
        if (to == Status) return new("student.status_unchanged", $"The student is already {to}.", "status");
        var allowed = (Status, to) switch
        {
            (StudentStatus.Active, _) => true,
            (StudentStatus.Suspended, StudentStatus.Active or StudentStatus.Withdrawn or StudentStatus.Graduated) => true,
            (StudentStatus.Withdrawn, StudentStatus.Active) => true,
            _ => false,
        };
        if (!allowed) return new("student.status_transition", $"A {Status.ToString().ToLowerInvariant()} student can't be marked {to.ToString().ToLowerInvariant()}.");
        Status = to;
        return null;
    }
}

/// <summary>A parent or guardian. One guardian can be linked to many students (siblings).</summary>
public class Guardian : Entity
{
    public required string FullName { get; set; }
    public required string Phone { get; set; }
    public string? Email { get; set; }
    public string? Occupation { get; set; }
    public string? Address { get; set; }
    public Guid? UserId { get; set; }
    public List<StudentGuardian> Students { get; set; } = [];

    /// <summary>
    /// Canonical form used to recognise the same parent across siblings: Nigerian local numbers become +234…
    /// ("0803 000 0000" → "+2348030000000"); other numbers keep their country code. Null if it can't be a phone number.
    /// </summary>
    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var trimmed = phone.Trim();
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (trimmed.Any(c => !char.IsDigit(c) && c is not (' ' or '-' or '(' or ')' or '+'))) return null;

        string normalized;
        if (trimmed.StartsWith('+')) normalized = "+" + digits;
        else if (digits.StartsWith("234")) normalized = "+" + digits;
        else if (digits.StartsWith('0') && digits.Length == 11) normalized = "+234" + digits[1..];
        else return null;

        return normalized.Length is >= 8 and <= 16 ? normalized : null;
    }
}

public class StudentGuardian
{
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public Guid GuardianId { get; set; }
    public Guardian Guardian { get; set; } = null!;
    public GuardianRelationship Relationship { get; set; }
    public bool IsPrimaryContact { get; set; }

    /// <summary>Authorised to collect the child from school.</summary>
    public bool CanPickUp { get; set; } = true;
}

public class Staff : Entity
{
    public required string StaffNumber { get; set; }
    public required string FullName { get; set; }
    public required string Email { get; set; }
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public string? Bio { get; set; }

    /// <summary>How the website shows the name (e.g. "Mrs. G. Okafor"); null = <see cref="FullName"/>.</summary>
    public string? WebsiteName { get; set; }
    public string? PhotoPath { get; set; }
    public bool ShowOnWebsite { get; set; }
    public int WebsiteOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? UserId { get; set; }
}
