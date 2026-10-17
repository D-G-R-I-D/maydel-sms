using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.GetExamResults;

/// <summary>Took the exam / passed / failed for a session (optionally one class), from the current cut-off (admin, E11.7).</summary>
public sealed record GetExamResultsQuery(Guid SessionId, Guid? ClassLevelId = null) : IQuery<ExamResults>;
