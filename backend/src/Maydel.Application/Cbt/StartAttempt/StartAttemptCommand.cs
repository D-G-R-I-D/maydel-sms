using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.StartAttempt;

/// <summary>
/// Starts the test, or resumes the attempt in progress on this device (E16.3). Resuming takes over from any other device: only the
/// newest session token can save (E16.6).
/// </summary>
public sealed record StartAttemptCommand(Guid TestId) : ICommand<AttemptPaper>;
