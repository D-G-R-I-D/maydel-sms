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
    public string? PhotoPath { get; set; }
    public bool ShowOnWebsite { get; set; }
    public int WebsiteOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? UserId { get; set; }
}
