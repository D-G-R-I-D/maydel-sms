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
}
