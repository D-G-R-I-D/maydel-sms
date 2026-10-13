using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Photos.RemoveStudentPhoto;

internal sealed class RemoveStudentPhotoHandler(IAppDbContext db, PhotoStore photos) : ICommandHandler<RemoveStudentPhotoCommand>
{
    public async Task<Result> Handle(RemoveStudentPhotoCommand c, CancellationToken ct)
    {
        var owner = await db.Students.FirstOrDefaultAsync(x => x.Id == c.StudentId, ct);
        if (owner is null) return Students.StudentErrors.NotFound;
        if (owner.PhotoPath is null) return PhotoErrors.NotFound;

        var previous = owner.PhotoPath;
        owner.PhotoPath = null;
        await db.SaveChangesAsync(ct);
        await photos.DeleteQuietlyAsync(previous, ct);
        return Result.Success();
    }
}
