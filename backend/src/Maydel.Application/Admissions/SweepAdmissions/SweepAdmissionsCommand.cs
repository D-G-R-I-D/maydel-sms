using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.SweepAdmissions;

/// <summary>
/// The admissions housekeeping run (by the worker): marks applications whose exam window closed unbooked as Missed, sends exam
/// reminders for sittings in the next 24 hours, deletes the child's details and documents from unsuccessful applications after
/// 6 months (NDPR), and deactivates applicant-only accounts unused for 12 months. <c>Now</c>/<c>SessionId</c> exist for tests.
/// </summary>
public sealed record SweepAdmissionsCommand(DateTimeOffset? Now = null, Guid? SessionId = null) : ICommand<SweepResult>;

public sealed record SweepResult(int Missed, int Reminders, int Purged = 0, int Deactivated = 0);
