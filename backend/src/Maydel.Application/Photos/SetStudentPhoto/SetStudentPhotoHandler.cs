using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Photos.SetStudentPhoto;

internal sealed class SetStudentPhotoHandler(IAppDbContext db, PhotoStore photos) : ICommandHandler<SetStudentPhotoCommand, PhotoInfo>
{
    public async Task<Result<PhotoInfo>> Handle(SetStudentPhotoCommand c, CancellationToken ct)
    {
        var owner = await db.Students.FirstOrDefaultAsync(x => x.Id == c.StudentId, ct);
        if (owner is null) return Students.StudentErrors.NotFound;

        var saved = await photos.SaveAsync(c.Content, "students", ct);
        if (saved.IsFailure) return saved.Error!;

        var previous = owner.PhotoPath;
        owner.PhotoPath = saved.Value.Key;
        await db.SaveChangesAsync(ct);
        await photos.DeleteQuietlyAsync(previous, ct);
        return new PhotoInfo(PhotoStore.VersionOf(owner.PhotoPath)!, saved.Value.Image.Width, saved.Value.Image.Height);
    }
}
