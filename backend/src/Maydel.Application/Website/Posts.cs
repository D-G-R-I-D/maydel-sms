using Maydel.Application.Abstractions;
using Maydel.Domain.Communication;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Website;

internal static class PostMapping
{
    public static PostDto ToDto(this Announcement a) => new(a.Id, a.Title, a.Slug, a.Summary, a.Body, a.Category, a.Audience, a.IsPinned, a.PublishedAt,
        a.ShowFrom, a.ShowUntil, a.IsFeatured, a.LinkUrl, a.LinkText, a.InTicker, a.InCarousel, WebsiteUrls.Image("news", a.Id, a.ImagePath), a.CreatedAt, a.UpdatedAt);

    public static void Apply(this Announcement a, PostInput i)
    {
        static string? C(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        (a.Title, a.Summary, a.Body, a.Category, a.Audience, a.IsPinned, a.ShowFrom, a.ShowUntil) =
            (i.Title ?? "", C(i.Summary), i.Body ?? "", C(i.Category), i.Audience, i.IsPinned, i.ShowFrom, i.ShowUntil);
        (a.IsFeatured, a.LinkUrl, a.LinkText, a.InTicker, a.InCarousel) = (i.IsFeatured, C(i.LinkUrl), C(i.LinkText), i.InTicker, i.InCarousel);
    }

    /// <summary>A unique slug for the title (adds -2, -3… when taken).</summary>
    public static async Task<string> UniqueSlugAsync(this IAppDbContext db, string title, Guid self, CancellationToken ct)
    {
        var root = Announcement.SlugFor(title);
        var taken = await db.Announcements.Where(a => a.Id != self && (a.Slug == root || a.Slug.StartsWith(root + "-"))).Select(a => a.Slug).ToListAsync(ct);
        if (!taken.Contains(root)) return root;
        for (var n = 2; ; n++)
            if (!taken.Contains($"{root}-{n}")) return $"{root}-{n}";
    }

    /// <summary>Published, public, newest first (pinned on top).</summary>
    public static IQueryable<Announcement> PublicPosts(this IAppDbContext db) =>
        db.Announcements.AsNoTracking().Where(a => a.PublishedAt != null && (a.Audience & Audience.Public) == Audience.Public);
}
