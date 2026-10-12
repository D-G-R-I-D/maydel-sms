using Maydel.Domain.Common;
using Maydel.Domain.People;

namespace Maydel.Domain.Academics;

public enum SchoolSection
{
    Creche,
    Nursery,
    Primary,
    JuniorSecondary,
    SeniorSecondary,
}

public enum TermNumber
{
    First = 1,
    Second = 2,
    Third = 3,
}

/// <summary>A school year, e.g. "2026/2027".</summary>
public class AcademicSession : Entity
{
    public required string Name { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public bool IsCurrent { get; set; }
    public List<Term> Terms { get; set; } = [];
}

public class Term : Entity
{
    public Guid SessionId { get; set; }
    public AcademicSession Session { get; set; } = null!;
    public TermNumber Number { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public DateOnly? MidTermStartsOn { get; set; }
    public DateOnly? MidTermEndsOn { get; set; }
    public bool IsCurrent { get; set; }

    public string DisplayName => $"{Number} Term {Session?.Name}".Trim();
}

/// <summary>A year group such as "Year 3" or "Nursery 1". <see cref="Order"/> drives promotion.</summary>
public class ClassLevel : Entity
{
    public required string Name { get; set; }
    public SchoolSection Section { get; set; }
    public int Order { get; set; }
    public List<ClassRoom> ClassRooms { get; set; } = [];
}

/// <summary>A class arm within a level, e.g. "Year 3 Gold". Students belong to a class room.</summary>
public class ClassRoom : Entity
{
    public Guid ClassLevelId { get; set; }
    public ClassLevel ClassLevel { get; set; } = null!;
    public required string Arm { get; set; }
    public Guid? FormTeacherId { get; set; }
    public Staff? FormTeacher { get; set; }
    public int Capacity { get; set; } = 30;
    public List<ClassSubject> Subjects { get; set; } = [];

    public string DisplayName => ClassLevel is null ? Arm : $"{ClassLevel.Name} {Arm}";
}

public class Subject : Entity
{
    public required string Name { get; set; }
    public required string Code { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>A subject offered in a class room, optionally taught by a specific teacher.</summary>
public class ClassSubject : Entity
{
    public Guid ClassRoomId { get; set; }
    public ClassRoom ClassRoom { get; set; } = null!;
    public Guid SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public Guid? TeacherId { get; set; }
    public Staff? Teacher { get; set; }
}
