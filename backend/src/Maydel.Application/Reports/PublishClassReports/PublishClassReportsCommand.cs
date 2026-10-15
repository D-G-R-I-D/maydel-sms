using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Reports.PublishClassReports;

/// <summary>
/// Publishes the term's reports for every active student in the class who has scores (E5.5): freezes average,
/// class average, position and class size, and locks scores, comments and attendance. Re-publishing recalculates.
/// Attendance must be entered first. Students without scores are skipped and listed.
/// <c>NotifyParents</c> (omitted or null = true) queues each guardian with an email a copy of the report (E12.3).
/// </summary>
public sealed record PublishClassReportsCommand(Guid ClassRoomId, Guid TermId, bool? NotifyParents = null) : ICommand<PublishResult>;
