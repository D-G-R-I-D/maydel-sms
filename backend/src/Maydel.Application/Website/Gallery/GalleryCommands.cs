using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Photos;
using Maydel.Domain.Communication;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Website.Gallery;

public sealed record ListPhotosQuery(string? Category = null) : IQuery<IReadOnlyList<GalleryPhotoDto>>;

/// <summary>Adds a photo (re-encoded JPEG up to 1600 px, EXIF stripped) with its details.</summary>
public sealed record AddPhotoCommand(byte[] File, PhotoInput Photo) : ICommand<GalleryPhotoDto>;

public sealed record UpdatePhotoCommand(Guid Id, PhotoInput Photo) : ICommand<GalleryPhotoDto>;

public sealed record DeletePhotoCommand(Guid Id) : ICommand;

internal static class GalleryMapping
{
    public static GalleryPhotoDto ToDto(this GalleryPhoto p) =>
        new(p.Id, p.Title, p.Caption, p.Category, p.Order, p.IsFeatured, WebsiteUrls.Image("gallery", p.Id, p.ImagePath)!, p.CreatedAt);

    public static Common.Error? Apply(this GalleryPhoto p, PhotoInput i)
    {
        var title = (i.Title ?? "").Trim();
        var category = (i.Category ?? "").Trim();
        if (title.Length is 0 or > 200) return Common.Error.Validation("title", "Give the photo a title (up to 200 characters).");
        if (category.Length is 0 or > 60) return Common.Error.Validation("category", "Choose an album, e.g. Sports day (up to 60 characters).");
        if (i.Caption is { Length: > 1000 }) return Common.Error.Validation("caption", "Keep the caption under 1000 characters.");
        (p.Title, p.Caption, p.Category, p.Order, p.IsFeatured) = (title, string.IsNullOrWhiteSpace(i.Caption) ? null : i.Caption.Trim(), category, i.Order, i.IsFeatured);
        return null;
    }
}

internal sealed class ListPhotosHandler(IAppDbContext db) : IQueryHandler<ListPhotosQuery, IReadOnlyList<GalleryPhotoDto>>
{
    public async Task<Result<IReadOnlyList<GalleryPhotoDto>>> Handle(ListPhotosQuery q, CancellationToken ct)
    {
        var photos = db.GalleryPhotos.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Category)) photos = photos.Where(p => p.Category == q.Category.Trim());
        return (await photos.OrderBy(p => p.Category).ThenBy(p => p.Order).ThenByDescending(p => p.CreatedAt).ToListAsync(ct)).Select(p => p.ToDto()).ToList();
    }
}

internal sealed class AddPhotoHandler(IAppDbContext db, PhotoStore store) : ICommandHandler<AddPhotoCommand, GalleryPhotoDto>
{
    public async Task<Result<GalleryPhotoDto>> Handle(AddPhotoCommand c, CancellationToken ct)
    {
        var photo = new GalleryPhoto { Title = "", Category = "", ImagePath = "" };
        if (photo.Apply(c.Photo) is { } invalid) return invalid;
        var saved = await store.SaveAsync(c.File, "gallery", transparent: false, 1600, ct);
        if (saved.IsFailure) return saved.Error!;
        photo.ImagePath = saved.Value.Key;
        db.GalleryPhotos.Add(photo);
        await db.SaveChangesAsync(ct);
        return photo.ToDto();
    }
}

internal sealed class UpdatePhotoHandler(IAppDbContext db) : ICommandHandler<UpdatePhotoCommand, GalleryPhotoDto>
{
    public async Task<Result<GalleryPhotoDto>> Handle(UpdatePhotoCommand c, CancellationToken ct)
    {
        if (await db.GalleryPhotos.FirstOrDefaultAsync(p => p.Id == c.Id, ct) is not { } photo) return WebsiteErrors.PhotoNotFound;
        if (photo.Apply(c.Photo) is { } invalid) return invalid;
        await db.SaveChangesAsync(ct);
        return photo.ToDto();
    }
}

internal sealed class DeletePhotoHandler(IAppDbContext db, PhotoStore store) : ICommandHandler<DeletePhotoCommand>
{
    public async Task<Result> Handle(DeletePhotoCommand c, CancellationToken ct)
    {
        if (await db.GalleryPhotos.FirstOrDefaultAsync(p => p.Id == c.Id, ct) is not { } photo) return WebsiteErrors.PhotoNotFound;
        db.GalleryPhotos.Remove(photo);
        await db.SaveChangesAsync(ct);
        await store.DeleteQuietlyAsync(photo.ImagePath, ct);
        return Result.Success();
    }
}
