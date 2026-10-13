using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Photos.RemoveStudentPhoto;

public sealed record RemoveStudentPhotoCommand(Guid StudentId) : ICommand;
