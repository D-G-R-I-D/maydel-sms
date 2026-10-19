using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.SaveAnswer;

/// <summary>Autosaves one answer (E16.3). Refused after the deadline (plus a few seconds' grace) or from a stale session.</summary>
public sealed record SaveAnswerCommand(Guid AttemptId, Guid ItemId, AnswerInput Answer) : ICommand<AnswerSaved>;
