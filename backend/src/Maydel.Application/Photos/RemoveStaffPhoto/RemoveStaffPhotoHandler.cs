using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Photos.RemoveStaffPhoto;

internal sealed class RemoveStaffPhotoHandler(IAppDbContext db, PhotoStore photos) : ICommandHandler<RemoveStaffPhotoCommand>
{
    public async Task<Result> Handle(RemoveStaffPhotoCommand c, CancellationToken ct)
    {
        var owner = await db.Staff.FirstOrDefaultAsync(x => x.Id == c.StaffId, ct);
        if (owner is null) return StaffMembers.StaffErrors.NotFound;
        if (owner.PhotoPath is null) return PhotoErrors.NotFound;

        var previous = owner.PhotoPath;
        owner.PhotoPath = null;
        await db.SaveChangesAsync(ct);
        await photos.DeleteQuietlyAsync(previous, ct);
        return Result.Success();
    }
}
