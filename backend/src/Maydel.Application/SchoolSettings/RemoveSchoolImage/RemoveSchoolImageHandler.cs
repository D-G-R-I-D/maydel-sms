using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Photos;

namespace Maydel.Application.SchoolSettings.RemoveSchoolImage;

internal sealed class RemoveSchoolImageHandler(IAppDbContext db, PhotoStore photos) : ICommandHandler<RemoveSchoolImageCommand, SchoolProfileDto>
{
    public async Task<Result<SchoolProfileDto>> Handle(RemoveSchoolImageCommand c, CancellationToken ct)
    {
        var profile = await db.CurrentAsync(ct);
        var previous = profile.PathOf(c.Image);
        profile.SetPath(c.Image, null);
        await db.SaveChangesAsync(ct);
        await photos.DeleteQuietlyAsync(previous, ct);
        return profile.ToDto();
    }
}
