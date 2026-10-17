using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.SweepAdmissions;

/// <summary>
/// The admissions housekeeping run (hourly, by the worker): marks applications whose exam window closed unbooked as Missed, and
/// sends exam reminders for sittings in the next 24 hours. <c>Now</c>/<c>SessionId</c> exist for tests.
/// </summary>
public sealed record SweepAdmissionsCommand(DateTimeOffset? Now = null, Guid? SessionId = null) : ICommand<SweepResult>;

public sealed record SweepResult(int Missed, int Reminders);
