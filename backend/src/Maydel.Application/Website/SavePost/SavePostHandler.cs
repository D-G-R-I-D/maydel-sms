using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Photos;
using Maydel.Domain.Communication;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Website.SavePost;

internal sealed class SavePostHandler(IAppDbContext db, ICurrentUser me) : ICommandHandler<SavePostCommand, PostDto>
{
    public async Task<Result<PostDto>> Handle(SavePostCommand c, CancellationToken ct)
    {
        Announcement post;
        if (c.Id is { } id)
        {
            if (await db.Announcements.FirstOrDefaultAsync(a => a.Id == id, ct) is not { } found) return WebsiteErrors.PostNotFound;
            post = found;
        }
        else
        {
            db.Announcements.Add(post = new Announcement { Title = "", Body = "", AuthorUserId = me.UserId });
        }
        post.Apply(c.Post);
        if (post.Check() is { } invalid) return invalid.ToError();
        if (post.PublishedAt is null || post.Slug.Length == 0) post.Slug = await db.UniqueSlugAsync(post.Title, post.Id, ct);
        await db.SaveChangesAsync(ct);
        return post.ToDto();
    }
}

internal sealed class PublishPostHandler(IAppDbContext db, ISchoolClock clock) : ICommandHandler<PublishPostCommand, PostDto>
{
    public async Task<Result<PostDto>> Handle(PublishPostCommand c, CancellationToken ct)
    {
        if (await db.Announcements.FirstOrDefaultAsync(a => a.Id == c.Id, ct) is not { } post) return WebsiteErrors.PostNotFound;
        post.PublishedAt = c.Publish ? post.PublishedAt ?? clock.Now.ToUniversalTime() : null;
        await db.SaveChangesAsync(ct);
        return post.ToDto();
    }
}

internal sealed class DeletePostHandler(IAppDbContext db, PhotoStore photos) : ICommandHandler<DeletePostCommand>
{
    public async Task<Result> Handle(DeletePostCommand c, CancellationToken ct)
    {
        if (await db.Announcements.FirstOrDefaultAsync(a => a.Id == c.Id, ct) is not { } post) return WebsiteErrors.PostNotFound;
        db.Announcements.Remove(post);
        await db.SaveChangesAsync(ct);
        await photos.DeleteQuietlyAsync(post.ImagePath, ct);
        return Result.Success();
    }
}

internal sealed class SetPostImageHandler(IAppDbContext db, PhotoStore photos) : ICommandHandler<SetPostImageCommand, PostDto>
{
    public async Task<Result<PostDto>> Handle(SetPostImageCommand c, CancellationToken ct)
    {
        if (await db.Announcements.FirstOrDefaultAsync(a => a.Id == c.Id, ct) is not { } post) return WebsiteErrors.PostNotFound;
        var old = post.ImagePath;
        if (c.File is { } file)
        {
            var saved = await photos.SaveAsync(file, "news", transparent: false, 1600, ct);
            if (saved.IsFailure) return saved.Error!;
            post.ImagePath = saved.Value.Key;
        }
        else
        {
            if (post.InCarousel) return Error.Validation("file", "This post is on the homepage carousel, which needs a picture. Take it off the carousel first.");
            post.ImagePath = null;
        }
        await db.SaveChangesAsync(ct);
        if (old is not null && old != post.ImagePath) await photos.DeleteQuietlyAsync(old, ct);
        return post.ToDto();
    }
}
