using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.MarkExamAbsent;

/// <summary>Marks a booked applicant absent: the application becomes Missed and the family is told it can be reopened.</summary>
public sealed record MarkExamAbsentCommand(Guid ApplicationId) : ICommand<ExamCandidate>;
