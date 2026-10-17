using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.ListExamCandidates;

/// <summary>Applicants booked into a sitting, by surname: the attendance sheet and score entry list (staff).</summary>
public sealed record ListExamCandidatesQuery(Guid SlotId) : IQuery<IReadOnlyList<ExamCandidate>>;
