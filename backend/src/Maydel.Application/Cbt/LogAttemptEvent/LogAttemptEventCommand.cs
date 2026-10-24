using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.LogAttemptEvent;

/// <summary>Records the test tab losing or regaining focus (E16.6), for the teacher to see alongside the marks.</summary>
public sealed record LogAttemptEventCommand(Guid AttemptId, IntegrityEventInput Event) : ICommand;
