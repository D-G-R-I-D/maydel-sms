using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.RecordExamScore;

/// <summary>Enters or corrects an applicant's entrance exam score (percentage, admin or teacher).</summary>
public sealed record RecordExamScoreCommand(Guid ApplicationId, decimal Score) : ICommand<ExamCandidate>;
