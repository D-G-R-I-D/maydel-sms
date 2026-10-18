using Maydel.Application.Common.Messaging;
using Maydel.Domain.Cbt;

namespace Maydel.Application.Cbt.ListTests;

/// <summary>Tests for a term that the caller can manage (teachers: their subjects and levels), soonest opening first.</summary>
public sealed record ListTestsQuery(Guid TermId, Guid? SubjectId = null, Guid? ClassLevelId = null, TestStatus? Status = null) : IQuery<IReadOnlyList<TestSummary>>;
