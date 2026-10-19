using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.GetAttemptReview;

/// <summary>One student's attempt for marking: answers next to the key or marking guide, marks so far, and the focus log.</summary>
public sealed record GetAttemptReviewQuery(Guid AttemptId) : IQuery<AttemptReview>;
