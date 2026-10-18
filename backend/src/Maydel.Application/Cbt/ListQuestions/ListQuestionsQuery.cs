using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Cbt;

namespace Maydel.Application.Cbt.ListQuestions;

/// <summary>The bank for a subject and class level, newest first; filter by kind, topic, difficulty or text.</summary>
public sealed record ListQuestionsQuery(
    Guid SubjectId, Guid ClassLevelId, QuestionKind? Kind = null, string? Topic = null, Difficulty? Difficulty = null, string? Search = null,
    int? Page = null, int? PageSize = null) : IQuery<PagedResult<QuestionDto>>;
