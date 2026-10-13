namespace Maydel.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

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
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Teacher = "Teacher";
    public const string Bursar = "Bursar";
    public const string Parent = "Parent";
    public const string Student = "Student";

    public static readonly IReadOnlyList<string> All = [Admin, Teacher, Bursar, Parent, Student];
    public static readonly IReadOnlyList<string> Staff = [Admin, Teacher, Bursar];
}
