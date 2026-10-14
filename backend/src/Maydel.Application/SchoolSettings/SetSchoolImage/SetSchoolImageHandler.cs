using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Photos;

namespace Maydel.Application.SchoolSettings.SetSchoolImage;

internal sealed class SetSchoolImageHandler(IAppDbContext db, PhotoStore photos) : ICommandHandler<SetSchoolImageCommand, SchoolProfileDto>
{
    public async Task<Result<SchoolProfileDto>> Handle(SetSchoolImageCommand c, CancellationToken ct)
    {
        var profile = await db.CurrentAsync(ct);
        var saved = await photos.SaveAsync(c.Content, "school", transparent: true, SchoolProfiles.MaxImageDimension, ct);
        if (saved.IsFailure) return saved.Error!;

        var previous = profile.PathOf(c.Image);
        profile.SetPath(c.Image, saved.Value.Key);
        await db.SaveChangesAsync(ct);
        await photos.DeleteQuietlyAsync(previous, ct);
        return profile.ToDto();
    }
}
