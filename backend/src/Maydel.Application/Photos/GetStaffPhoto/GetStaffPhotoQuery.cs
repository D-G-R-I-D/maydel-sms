using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Photos.GetStaffPhoto;

/// <summary>The staff member's photo bytes, after the same access check as the staff member record.</summary>
public sealed record GetStaffPhotoQuery(Guid StaffId) : IQuery<PhotoContent>;
