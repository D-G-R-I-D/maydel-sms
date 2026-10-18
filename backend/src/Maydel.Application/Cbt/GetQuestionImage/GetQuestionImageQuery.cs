using Maydel.Application.Common.Messaging;
using Maydel.Application.Photos;

namespace Maydel.Application.Cbt.GetQuestionImage;

/// <summary>The question's picture for the staff who can see the question (students get it through their attempt, E16.3).</summary>
public sealed record GetQuestionImageQuery(Guid Id) : IQuery<PhotoContent>;
