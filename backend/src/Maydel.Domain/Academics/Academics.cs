using System.Text.RegularExpressions;
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
public partial class AcademicSession : Entity
{
    public required string Name { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public bool IsCurrent { get; set; }
    public List<Term> Terms { get; set; } = [];

    /// <summary>Name is "YYYY/YYYY" with consecutive years; the session ends after it starts.</summary>
    public static DomainError? Validate(string name, DateOnly startsOn, DateOnly endsOn)
    {
        var match = SessionName().Match(name);
        if (!match.Success || int.Parse(match.Groups[2].Value) != int.Parse(match.Groups[1].Value) + 1)
            return new("session.name_format", "Use the form 2026/2027 (two consecutive years).", "name");
        if (endsOn <= startsOn)
            return new("session.dates", "The session must end after it starts.", "endsOn");
        return null;
    }

    public DomainError? Reschedule(string name, DateOnly startsOn, DateOnly endsOn)
    {
        if (Validate(name, startsOn, endsOn) is { } error) return error;
        if (Terms.Any(t => t.StartsOn < startsOn || t.EndsOn > endsOn))
            return new("session.terms_outside", "Some terms fall outside these dates. Adjust the terms first.", "startsOn");
        (Name, StartsOn, EndsOn) = (name, startsOn, endsOn);
        return null;
    }

    /// <summary>Adds a term: one per number, inside the session, not overlapping other terms.</summary>
    public DomainError? AddTerm(Term term)
    {
        if (Terms.Any(t => t.Number == term.Number))
            return new("term.duplicate", $"{term.Number} term already exists for {Name}.", "number");
        if (CheckTermDates(term, term.StartsOn, term.EndsOn, term.MidTermStartsOn, term.MidTermEndsOn) is { } error) return error;
        Terms.Add(term);
        return null;
    }

    public DomainError? RescheduleTerm(Term term, DateOnly startsOn, DateOnly endsOn, DateOnly? midTermStartsOn, DateOnly? midTermEndsOn)
    {
        if (CheckTermDates(term, startsOn, endsOn, midTermStartsOn, midTermEndsOn) is { } error) return error;
        (term.StartsOn, term.EndsOn, term.MidTermStartsOn, term.MidTermEndsOn) = (startsOn, endsOn, midTermStartsOn, midTermEndsOn);
        return null;
    }

    private DomainError? CheckTermDates(Term term, DateOnly startsOn, DateOnly endsOn, DateOnly? midStart, DateOnly? midEnd)
    {
        if (endsOn <= startsOn) return new("term.dates", "The term must end after it starts.", "endsOn");
        if (startsOn < StartsOn || endsOn > EndsOn)
            return new("term.outside_session", $"The term must fall within the session ({StartsOn:d MMM yyyy} – {EndsOn:d MMM yyyy}).", "startsOn");
        if (midStart is null != midEnd is null)
            return new("term.midterm_incomplete", "Give both the start and end of the mid-term break, or neither.", "midTermEndsOn");
        if (midStart is { } ms && midEnd is { } me && (me < ms || ms < startsOn || me > endsOn))
            return new("term.midterm_dates", "The mid-term break must fall within the term and end on or after it starts.", "midTermStartsOn");
        if (Terms.Any(t => t != term && t.StartsOn <= endsOn && startsOn <= t.EndsOn))
            return new("term.overlap", "This term overlaps another term in the session.", "startsOn");
        if (Terms.Any(t => t != term && ((t.Number < term.Number && t.StartsOn > startsOn) || (t.Number > term.Number && t.StartsOn < startsOn))))
            return new("term.order", "Terms must be in order: First, then Second, then Third.", "startsOn");
        return null;
    }

    [GeneratedRegex(@"^(\d{4})/(\d{4})$")]
    private static partial Regex SessionName();
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
    public const int MaxCapacity = 200;

    public Guid ClassLevelId { get; set; }
    public ClassLevel ClassLevel { get; set; } = null!;
    public required string Arm { get; set; }
    public Guid? FormTeacherId { get; set; }
    public Staff? FormTeacher { get; set; }
    public int Capacity { get; set; } = 30;
    public List<ClassSubject> Subjects { get; set; } = [];

    public string DisplayName => ClassLevel is null ? Arm : $"{ClassLevel.Name} {Arm}";

    /// <summary>Arm is required; capacity 1..200 and never below the number of students already enrolled.</summary>
    public static DomainError? Validate(string arm, int capacity, int enrolled = 0)
    {
        if (string.IsNullOrWhiteSpace(arm)) return new("classroom.arm_required", "Give the arm a name, e.g. A or Gold.", "arm");
        if (capacity is < 1 or > MaxCapacity) return new("classroom.capacity", $"Capacity must be between 1 and {MaxCapacity}.", "capacity");
        if (capacity < enrolled) return new("classroom.capacity_below_enrolment", $"{enrolled} students are already in this class; capacity can't be lower.", "capacity");
        return null;
    }
}

public class Subject : Entity
{
    public required string Name { get; set; }
    public required string Code { get; set; }
    public bool IsActive { get; set; } = true;

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
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
