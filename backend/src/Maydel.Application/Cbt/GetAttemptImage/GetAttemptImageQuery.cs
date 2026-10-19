using Maydel.Application.Common.Messaging;
using Maydel.Application.Photos;

namespace Maydel.Application.Cbt.GetAttemptImage;

/// <summary>A question's picture, for the student whose paper it's on.</summary>
public sealed record GetAttemptImageQuery(Guid AttemptId, Guid ItemId) : IQuery<PhotoContent>;
