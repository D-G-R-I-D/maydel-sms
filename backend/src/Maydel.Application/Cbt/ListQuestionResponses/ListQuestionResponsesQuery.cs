using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.ListQuestionResponses;

/// <summary>Every handed-in answer to one question in a test, for marking question by question (consistent marking).</summary>
public sealed record ListQuestionResponsesQuery(Guid TestId, Guid QuestionId) : IQuery<QuestionResponses>;
