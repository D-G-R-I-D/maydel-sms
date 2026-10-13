using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Reports.SaveAttendance;

/// <summary>
/// Days the school opened this term (class-wide) and days each listed student was present, for the report card.
/// Entered by the form teacher or an admin until the daily register (E9) fills this automatically.
/// </summary>
public sealed record SaveAttendanceCommand(Guid ClassRoomId, Guid TermId, int DaysOpened, IReadOnlyList<AttendanceEntry> Entries) : ICommand<ClassReportSummary>;
