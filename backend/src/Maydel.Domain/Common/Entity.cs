namespace Maydel.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>
/// A violated invariant, returned (not thrown) by domain methods. With <see cref="Field"/> it's a field validation
/// error (HTTP 400), otherwise a business-rule failure (HTTP 422). <see cref="Message"/> is shown to users.
/// </summary>
public sealed record DomainError(string Code, string Message, string? Field = null);

/// <summary>Raised when a business rule is violated. The API maps it to HTTP 422.</summary>
public class DomainException(string message) : Exception(message);

/// <summary>The fixed set of roles. Serialised by name; <see cref="Roles"/> holds the same names as constants for policies.</summary>
public enum Role
{
    Admin,
    Teacher,
    Bursar,
    Parent,
    Student,
    /// <summary>A family applying for admission (E11). Becomes Parent too once a child is enrolled.</summary>
    Applicant,
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Teacher = "Teacher";
    public const string Bursar = "Bursar";
    public const string Parent = "Parent";
    public const string Student = "Student";
    public const string Applicant = "Applicant";

    public static readonly IReadOnlyList<string> All = [Admin, Teacher, Bursar, Parent, Student, Applicant];
    public static readonly IReadOnlyList<string> Staff = [Admin, Teacher, Bursar];
}
