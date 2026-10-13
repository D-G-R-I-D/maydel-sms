using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Photos.GetStudentPhoto;

/// <summary>The student's photo bytes, after the same access check as the student record.</summary>
public sealed record GetStudentPhotoQuery(Guid StudentId) : IQuery<PhotoContent>;
