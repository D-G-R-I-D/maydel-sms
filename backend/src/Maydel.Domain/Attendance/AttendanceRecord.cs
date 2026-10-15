using Maydel.Domain.Academics;
using Maydel.Domain.Common;
using Maydel.Domain.People;

namespace Maydel.Domain.Attendance;

public enum AttendanceStatus
{
    Present,
    Absent,
    Late,
    Excused,
}

/// <summary>Daily register entry. Unique per student per date.</summary>
public class AttendanceRecord : Entity
{
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public Guid ClassRoomId { get; set; }
    public DateOnly Date { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Note { get; set; }
    public Guid? RecordedByUserId { get; set; }

    public bool CountsAsPresent => Status is AttendanceStatus.Present or AttendanceStatus.Late;

    /// <summary>Notes are optional, trimmed and kept short; a blank note is stored as none.</summary>
    public void Set(AttendanceStatus status, string? note, Guid? by) =>
        (Status, Note, RecordedByUserId) = (status, string.IsNullOrWhiteSpace(note) ? null : note.Trim(), by);
}

/// <summary>Daily register rules (E9.1). Pure, unit-tested.</summary>
public static class RegisterRules
{
    /// <summary>Nigeria has no daylight saving: school days are counted in West Africa Time (UTC+1).</summary>
    public static readonly TimeSpan SchoolUtcOffset = TimeSpan.FromHours(1);

    public static DateOnly TodayAt(DateTimeOffset utcNow) => DateOnly.FromDateTime(utcNow.ToOffset(SchoolUtcOffset).DateTime);

    /// <summary>Why <paramref name="date"/> can't have a register, or null for a school day in <paramref name="term"/>.</summary>
    public static DomainError? CheckSchoolDay(DateOnly date, Term? term)
    {
        if (term is null) return new("attendance.not_in_term", "That date isn't in any term.");
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return new("attendance.weekend", "There's no register at weekends.");
        if (term.MidTermStartsOn is { } from && term.MidTermEndsOn is { } to && date >= from && date <= to)
            return new("attendance.mid_term", "That date is in the mid-term break.");
        return null;
    }

    /// <summary>The form teacher takes today's register; only an admin corrects earlier days. Nobody records the future.</summary>
    public static DomainError? CheckEditable(DateOnly date, DateOnly today, bool isAdmin)
    {
        if (date > today) return new("attendance.future", "You can't take the register for a future date.");
        if (!isAdmin && date != today) return new("attendance.past_day", "Only an administrator can change the register for an earlier day.");
        return null;
    }
}
