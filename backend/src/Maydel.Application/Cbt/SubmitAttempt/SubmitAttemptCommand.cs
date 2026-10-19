using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.SubmitAttempt;

/// <summary>Hands the test in. Objective parts are marked now; scores stay hidden until the teacher releases results (E16.4).</summary>
public sealed record SubmitAttemptCommand(Guid AttemptId, string SessionToken) : ICommand<AttemptPaper>;
