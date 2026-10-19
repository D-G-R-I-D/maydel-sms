using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.GetAttempt;

/// <summary>The student's paper with their saved answers (no keys; no session token: start/resume issues that).</summary>
public sealed record GetAttemptQuery(Guid AttemptId) : IQuery<AttemptPaper>;
