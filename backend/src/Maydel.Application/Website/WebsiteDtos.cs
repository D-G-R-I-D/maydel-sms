using Maydel.Application.Abstractions;
using Maydel.Domain.Admissions;
using Maydel.Domain.Communication;

namespace Maydel.Application.Website;

// ---------- Admin (CMS) ----------

/// <summary><c>BodyHtml</c>: formatted content from the editor; when given, <c>Body</c> may be empty and is derived from it.</summary>
public sealed record PostInput(
    string Title, string? Summary, string Body, string? Category, Audience Audience, bool IsPinned, DateOnly? ShowFrom, DateOnly? ShowUntil,
    bool IsFeatured, string? LinkUrl, string? LinkText, bool InTicker, bool InCarousel, string? BodyHtml = null);

/// <summary>A news post or notice as staff edit it. <c>ImageUrl</c> is a public URL once published.</summary>
public sealed record PostDto(
    Guid Id, string Title, string Slug, string? Summary, string Body, string? Category, Audience Audience, bool IsPinned, DateTimeOffset? PublishedAt,
    DateOnly? ShowFrom, DateOnly? ShowUntil, bool IsFeatured, string? LinkUrl, string? LinkText, bool InTicker, bool InCarousel, string? ImageUrl,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, string? BodyHtml);

public sealed record EventInput(string Title, string? Description, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, string? Location, Audience Audience,
    string? DescriptionHtml = null);

public sealed record EventDto(Guid Id, string Title, string? Description, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, string? Location, Audience Audience,
    string? DescriptionHtml);

public sealed record PhotoInput(string Title, string? Caption, string Category, int Order, bool IsFeatured);

public sealed record GalleryPhotoDto(Guid Id, string Title, string? Caption, string Category, int Order, bool IsFeatured, string ImageUrl, DateTimeOffset CreatedAt);

public sealed record ContactMessageDto(Guid Id, string Name, string Email, string? Phone, string? Subject, string Message, MessageStatus Status, DateTimeOffset CreatedAt);

/// <summary><c>DisplayName</c>: the name on the website (e.g. "Mrs. G. Okafor"); empty = the staff record's full name.</summary>
public sealed record StaffWebsiteInput(bool ShowOnWebsite, string? JobTitle, string? Bio, int WebsiteOrder, string? DisplayName = null);

// ---------- Public ----------

public sealed record NewsSummary(Guid Id, string Slug, string Title, string? Summary, string? Category, bool IsPinned, DateTimeOffset PublishedAt, string? ImageUrl);

/// <summary><c>BodyHtml</c>: cleaned formatted content to render as HTML when present; otherwise show <c>Body</c> as text.</summary>
public sealed record NewsArticle(Guid Id, string Slug, string Title, string? Summary, string Body, string? Category, DateTimeOffset PublishedAt,
    DateTimeOffset? UpdatedAt, string? ImageUrl, string? LinkUrl, string? LinkText, string? BodyHtml);

/// <summary>A current public notice for the envelope (E7.10). <c>Version</c> changes when it's edited, so "seen" can reset.</summary>
public sealed record PublicNotice(Guid Id, string Title, string? Summary, string Body, string? LinkUrl, string? LinkText, bool IsFeatured,
    DateTimeOffset PublishedAt, string Version);

public enum SlideKind
{
    News,
    Photo,
}

public sealed record Slide(SlideKind Kind, Guid Id, string Title, string? Caption, string ImageUrl, string? Href);

public sealed record TickerItem(Guid Id, string Text, string? Href);

/// <summary>The homepage carousel (featured news with pictures, then featured photos) and ticker (E7.9).</summary>
public sealed record Highlights(IReadOnlyList<Slide> Slides, IReadOnlyList<TickerItem> Ticker);

public sealed record PublicEvent(Guid Id, string Title, string? Description, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, string? Location, string? DescriptionHtml);

public sealed record PublicPhoto(Guid Id, string Title, string? Caption, string Category, string ImageUrl);

public sealed record PublicGallery(IReadOnlyList<string> Categories, IReadOnlyList<PublicPhoto> Photos);

public sealed record PublicStaff(Guid Id, string Name, string? JobTitle, string? Bio, string? PhotoUrl);

public sealed record ContactInput(string Name, string Email, string? Phone, string? Subject, string Message, string? Website);

public static class WebsiteErrors
{
    public static readonly Common.Error PostNotFound = Common.Error.NotFound("news.not_found", "That post doesn't exist.");
    public static readonly Common.Error EventNotFound = Common.Error.NotFound("event.not_found", "That event doesn't exist.");
    public static readonly Common.Error PhotoNotFound = Common.Error.NotFound("gallery.not_found", "That photo doesn't exist.");
    public static readonly Common.Error MessageNotFound = Common.Error.NotFound("contact.not_found", "That message doesn't exist.");
}

/// <summary>Public image URLs: versioned by the stored file name so they can be cached for a long time.</summary>
public static class WebsiteUrls
{
    public static string? Image(string kind, Guid id, string? key) => key is null ? null : $"/api/public/site/images/{kind}/{id}?v={Photos.PhotoStore.VersionOf(key)}";
}
