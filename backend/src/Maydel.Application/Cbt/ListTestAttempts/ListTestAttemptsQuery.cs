using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.ListTestAttempts;

/// <summary>Everyone in the test's classes with how they got on: not started, sitting, handed in, marks so far and focus log counts.</summary>
public sealed record ListTestAttemptsQuery(Guid TestId) : IQuery<AttemptRows>;
