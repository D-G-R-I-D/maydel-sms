using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Reports.PublishClassReports;

/// <summary>
/// Publishes the term's reports for every active student in the class who has scores and isn't published yet (E5.5):
/// freezes average, class average, position and class size, and locks scores, comments and attendance. Reports that are
/// already published keep their publication date but get their position and class average refreshed, since newcomers can
/// shift them. Refused (422 <c>report.already_published</c>) when nothing is left to publish, so pressing Publish twice
/// never re-sends anything. Attendance must be entered first. Students without scores are skipped and listed.
/// <c>NotifyParents</c> (omitted or null = true) emails the guardians of the newly published students only (E12.3).
/// </summary>
public sealed record PublishClassReportsCommand(Guid ClassRoomId, Guid TermId, bool? NotifyParents = null) : ICommand<PublishResult>;
