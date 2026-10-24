using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Photos;
using Maydel.Domain.Communication;

namespace Maydel.Application.Website.Media;

/// <summary>Where an uploaded picture can be placed: the editor puts <c>Url</c> in an image's src.</summary>
public sealed record MediaDto(Guid Id, string Url);

/// <summary>Uploads a picture for formatted content (resized to 1600 px). It's public by its address straight away.</summary>
public sealed record UploadMediaCommand(byte[] File) : ICommand<MediaDto>;

internal sealed class UploadMediaHandler(IAppDbContext db, PhotoStore photos) : ICommandHandler<UploadMediaCommand, MediaDto>
{
    public async Task<Result<MediaDto>> Handle(UploadMediaCommand c, CancellationToken ct)
    {
        var saved = await photos.SaveAsync(c.File, "media", transparent: false, 1600, ct);
        if (saved.IsFailure) return saved.Error!;
        var asset = new MediaAsset { ImagePath = saved.Value.Key };
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync(ct);
        return new MediaDto(asset.Id, WebsiteUrls.Image("media", asset.Id, asset.ImagePath)!);
    }
}
