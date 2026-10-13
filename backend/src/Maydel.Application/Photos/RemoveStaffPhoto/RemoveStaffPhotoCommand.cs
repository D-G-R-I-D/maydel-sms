using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Photos.RemoveStaffPhoto;

public sealed record RemoveStaffPhotoCommand(Guid StaffId) : ICommand;
