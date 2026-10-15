using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Attendance.GetAttendanceSummary;

/// <summary>Term attendance per student for a class (E9.2), from the daily register.</summary>
public sealed record GetAttendanceSummaryQuery(Guid ClassRoomId, Guid TermId) : IQuery<ClassAttendanceSummary>;
