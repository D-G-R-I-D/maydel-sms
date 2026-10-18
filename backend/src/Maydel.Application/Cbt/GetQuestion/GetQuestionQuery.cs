using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.GetQuestion;

public sealed record GetQuestionQuery(Guid Id) : IQuery<QuestionDto>;
