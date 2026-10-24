using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Photos;
using Maydel.Domain.Communication;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Website.Public;

public sealed record ListNewsQuery(string? Category = null, int? Page = null, int? PageSize = null) : IQuery<PagedResult<NewsSummary>>;
public sealed record GetNewsQuery(string Slug) : IQuery<NewsArticle>;
public sealed record ListNoticesQuery : IQuery<IReadOnlyList<PublicNotice>>;
public sealed record GetHighlightsQuery : IQuery<Highlights>;
public sealed record ListPublicEventsQuery(int Take = 20) : IQuery<IReadOnlyList<PublicEvent>>;
public sealed record GetGalleryQuery(string? Category = null) : IQuery<PublicGallery>;
public sealed record ListPublicStaffQuery : IQuery<IReadOnlyList<PublicStaff>>;

public enum PublicImageKind
{
    News,
    Gallery,
    Staff,
    Media,
}

/// <summary>A picture that's on the public website (published public post, gallery photo, or staff shown on the site).</summary>
public sealed record GetPublicImageQuery(PublicImageKind Kind, Guid Id) : IQuery<PhotoContent>;

internal sealed class ListNewsHandler(IAppDbContext db) : IQueryHandler<ListNewsQuery, PagedResult<NewsSummary>>
{
    public async Task<Result<PagedResult<NewsSummary>>> Handle(ListNewsQuery q, CancellationToken ct)
    {
        var posts = db.PublicPosts();
        if (!string.IsNullOrWhiteSpace(q.Category)) posts = posts.Where(a => a.Category == q.Category.Trim());
        var page = await posts.OrderByDescending(a => a.IsPinned).ThenByDescending(a => a.PublishedAt).ToPagedAsync(q.Page, q.PageSize, ct);
        return new PagedResult<NewsSummary>([.. page.Items.Select(a => new NewsSummary(a.Id, a.Slug, a.Title, a.Summary, a.Category, a.IsPinned, a.PublishedAt!.Value,
            WebsiteUrls.Image("news", a.Id, a.ImagePath)))], page.Page, page.PageSize, page.TotalCount);
    }
}

internal sealed class GetNewsHandler(IAppDbContext db) : IQueryHandler<GetNewsQuery, NewsArticle>
{
    public async Task<Result<NewsArticle>> Handle(GetNewsQuery q, CancellationToken ct) =>
        await db.PublicPosts().FirstOrDefaultAsync(a => a.Slug == q.Slug, ct) is { } a
            ? new NewsArticle(a.Id, a.Slug, a.Title, a.Summary, a.Body, a.Category, a.PublishedAt!.Value, a.UpdatedAt, WebsiteUrls.Image("news", a.Id, a.ImagePath),
                a.LinkUrl, a.LinkText, a.BodyHtml)
            : WebsiteErrors.PostNotFound;
}

internal sealed class ListNoticesHandler(IAppDbContext db, ISchoolClock clock) : IQueryHandler<ListNoticesQuery, IReadOnlyList<PublicNotice>>
{
    public async Task<Result<IReadOnlyList<PublicNotice>>> Handle(ListNoticesQuery q, CancellationToken ct)
    {
        var today = clock.Today;
        // Notices are posts with a window or a pop-up; plain news stays in the news section.
        var posts = await db.PublicPosts()
            .Where(a => (a.ShowFrom != null || a.ShowUntil != null || a.IsFeatured) && (a.ShowFrom == null || a.ShowFrom <= today) && (a.ShowUntil == null || a.ShowUntil >= today))
            .OrderByDescending(a => a.IsFeatured).ThenByDescending(a => a.PublishedAt).Take(20).ToListAsync(ct);
        return posts.Select(a => new PublicNotice(a.Id, a.Title, a.Summary, a.Body, a.LinkUrl, a.LinkText, a.IsFeatured, a.PublishedAt!.Value,
            (a.UpdatedAt ?? a.CreatedAt).ToUnixTimeMilliseconds().ToString())).ToList();
    }
}

internal sealed class GetHighlightsHandler(IAppDbContext db, ISchoolClock clock) : IQueryHandler<GetHighlightsQuery, Highlights>
{
    public async Task<Result<Highlights>> Handle(GetHighlightsQuery q, CancellationToken ct)
    {
        var today = clock.Today;
        var posts = await db.PublicPosts().Where(a => (a.InCarousel || a.InTicker) && (a.ShowFrom == null || a.ShowFrom <= today) && (a.ShowUntil == null || a.ShowUntil >= today))
            .OrderByDescending(a => a.IsPinned).ThenByDescending(a => a.PublishedAt).Take(30).ToListAsync(ct);
        var photos = await db.GalleryPhotos.AsNoTracking().Where(p => p.IsFeatured).OrderBy(p => p.Order).ThenByDescending(p => p.CreatedAt).Take(10).ToListAsync(ct);
        var slides = posts.Where(a => a.InCarousel && a.ImagePath is not null)
            .Select(a => new Slide(SlideKind.News, a.Id, a.Title, a.Summary, WebsiteUrls.Image("news", a.Id, a.ImagePath)!, a.LinkUrl ?? $"/news/{a.Slug}"))
            .Concat(photos.Select(p => new Slide(SlideKind.Photo, p.Id, p.Title, p.Caption, WebsiteUrls.Image("gallery", p.Id, p.ImagePath)!, "/gallery")))
            .Take(10).ToList();
        var ticker = posts.Where(a => a.InTicker).Select(a => new TickerItem(a.Id, a.Summary ?? a.Title, a.LinkUrl ?? $"/news/{a.Slug}")).Take(10).ToList();
        return new Highlights(slides, ticker);
    }
}

internal sealed class ListPublicEventsHandler(IAppDbContext db, ISchoolClock clock) : IQueryHandler<ListPublicEventsQuery, IReadOnlyList<PublicEvent>>
{
    public async Task<Result<IReadOnlyList<PublicEvent>>> Handle(ListPublicEventsQuery q, CancellationToken ct)
    {
        var now = clock.Now.ToUniversalTime();
        return await db.Events.AsNoTracking()
            .Where(e => (e.Audience & Audience.Public) == Audience.Public && (e.EndsAt ?? e.StartsAt) >= now)
            .OrderBy(e => e.StartsAt).Take(Math.Clamp(q.Take, 1, 100))
            .Select(e => new PublicEvent(e.Id, e.Title, e.Description, e.StartsAt, e.EndsAt, e.Location, e.DescriptionHtml)).ToListAsync(ct);
    }
}

internal sealed class GetGalleryHandler(IAppDbContext db) : IQueryHandler<GetGalleryQuery, PublicGallery>
{
    public async Task<Result<PublicGallery>> Handle(GetGalleryQuery q, CancellationToken ct)
    {
        var categories = await db.GalleryPhotos.AsNoTracking().Select(p => p.Category).Distinct().OrderBy(c => c).ToListAsync(ct);
        var photos = db.GalleryPhotos.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Category)) photos = photos.Where(p => p.Category == q.Category.Trim());
        var list = await photos.OrderBy(p => p.Category).ThenBy(p => p.Order).ThenByDescending(p => p.CreatedAt).Take(500).ToListAsync(ct);
        return new PublicGallery(categories, [.. list.Select(p => new PublicPhoto(p.Id, p.Title, p.Caption, p.Category, WebsiteUrls.Image("gallery", p.Id, p.ImagePath)!))]);
    }
}

internal sealed class ListPublicStaffHandler(IAppDbContext db) : IQueryHandler<ListPublicStaffQuery, IReadOnlyList<PublicStaff>>
{
    public async Task<Result<IReadOnlyList<PublicStaff>>> Handle(ListPublicStaffQuery q, CancellationToken ct) =>
        (await db.Staff.AsNoTracking().Where(s => s.ShowOnWebsite && s.IsActive).OrderBy(s => s.WebsiteOrder).ThenBy(s => s.FullName).ToListAsync(ct))
            .Select(s => new PublicStaff(s.Id, s.FullName, s.JobTitle, s.Bio, WebsiteUrls.Image("staff", s.Id, s.PhotoPath))).ToList();
}

internal sealed class GetPublicImageHandler(IAppDbContext db, PhotoStore photos) : IQueryHandler<GetPublicImageQuery, PhotoContent>
{
    public async Task<Result<PhotoContent>> Handle(GetPublicImageQuery q, CancellationToken ct)
    {
        var key = q.Kind switch
        {
            PublicImageKind.News => await db.PublicPosts().Where(a => a.Id == q.Id).Select(a => a.ImagePath).FirstOrDefaultAsync(ct),
            PublicImageKind.Gallery => await db.GalleryPhotos.AsNoTracking().Where(p => p.Id == q.Id).Select(p => p.ImagePath).FirstOrDefaultAsync(ct),
            PublicImageKind.Media => await db.MediaAssets.AsNoTracking().Where(m => m.Id == q.Id).Select(m => m.ImagePath).FirstOrDefaultAsync(ct),
            // Only staff the school chose to show: everyone else's photo stays private.
            _ => await db.Staff.AsNoTracking().Where(s => s.Id == q.Id && s.ShowOnWebsite && s.IsActive).Select(s => s.PhotoPath).FirstOrDefaultAsync(ct),
        };
        return await photos.OpenAsync(key, ct);
    }
}
