using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Photos.SetStudentPhoto;

/// <summary>Uploads or replaces the student's photo. The image is re-encoded (metadata stripped, max 800 px) before it is stored.</summary>
public sealed record SetStudentPhotoCommand(Guid StudentId, byte[] Content) : ICommand<PhotoInfo>;
