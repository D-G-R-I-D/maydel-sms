using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Photos.SetStaffPhoto;

/// <summary>Uploads or replaces the staff member's photo. The image is re-encoded (metadata stripped, max 800 px) before it is stored.</summary>
public sealed record SetStaffPhotoCommand(Guid StaffId, byte[] Content) : ICommand<PhotoInfo>;
