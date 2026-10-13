using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Photos.SetStaffPhoto;

internal sealed class SetStaffPhotoHandler(IAppDbContext db, PhotoStore photos) : ICommandHandler<SetStaffPhotoCommand, PhotoInfo>
{
    public async Task<Result<PhotoInfo>> Handle(SetStaffPhotoCommand c, CancellationToken ct)
    {
        var owner = await db.Staff.FirstOrDefaultAsync(x => x.Id == c.StaffId, ct);
        if (owner is null) return StaffMembers.StaffErrors.NotFound;

        var saved = await photos.SaveAsync(c.Content, "staff", ct);
        if (saved.IsFailure) return saved.Error!;

        var previous = owner.PhotoPath;
        owner.PhotoPath = saved.Value.Key;
        await db.SaveChangesAsync(ct);
        await photos.DeleteQuietlyAsync(previous, ct);
        return new PhotoInfo(PhotoStore.VersionOf(owner.PhotoPath)!, saved.Value.Image.Width, saved.Value.Image.Height);
    }
}
