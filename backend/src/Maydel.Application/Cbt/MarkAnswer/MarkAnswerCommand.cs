using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.MarkAnswer;

/// <summary>Marks one answer (theory) or overrides the computer's mark, with an optional comment for the student (E16.4).</summary>
public sealed record MarkAnswerCommand(Guid AttemptId, Guid ItemId, MarkInput Mark) : ICommand<AttemptReview>;
