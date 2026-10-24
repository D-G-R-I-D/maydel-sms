using Maydel.Domain.Common;

namespace Maydel.Domain.Communication;

[Flags]
public enum Audience
{
    None = 0,
    Public = 1,
    Parents = 2,
    Students = 4,
    Staff = 8,
    Everyone = Public | Parents | Students | Staff,
}

/// <summary>
/// News and notices (E7.3, E7.9, E7.10, E12.1). Public ones appear on the website: in the news section, the envelope of notices
/// (inside their date window), the homepage carousel and the ticker. Drafts until published.
/// </summary>
public partial class Announcement : Entity
{
    public required string Title { get; set; }

    /// <summary>The web address part, e.g. "inter-house-sports-2026"; fixed once published so links keep working.</summary>
    public string Slug { get; set; } = "";
    public string? Summary { get; set; }
    public required string Body { get; set; }
    public string? Category { get; set; }
    public string? ImagePath { get; set; }
    public Audience Audience { get; set; } = Audience.Everyone;
    public bool IsPinned { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public Guid? AuthorUserId { get; set; }

    /// <summary>Notice window: shown in the envelope (and pops up when featured) from/until these days; null = no limit.</summary>
    public DateOnly? ShowFrom { get; set; }
    public DateOnly? ShowUntil { get; set; }

    /// <summary>Pops up once per visitor (until edited) as a dismissible message (E7.10).</summary>
    public bool IsFeatured { get; set; }
    public string? LinkUrl { get; set; }
    public string? LinkText { get; set; }
    public bool InTicker { get; set; }
    public bool InCarousel { get; set; }

    public bool IsPublic => PublishedAt is not null && Audience.HasFlag(Audience.Public);

    public bool IsShowingOn(DateOnly today) => (ShowFrom is null || ShowFrom <= today) && (ShowUntil is null || ShowUntil >= today);

    [System.Text.RegularExpressions.GeneratedRegex("[^a-z0-9]+")]
    private static partial System.Text.RegularExpressions.Regex NonSlug();

    /// <summary>"Inter-House Sports 2026!" → "inter-house-sports-2026".</summary>
    public static string SlugFor(string title)
    {
        var slug = NonSlug().Replace(title.Trim().ToLowerInvariant(), "-").Trim('-');
        if (slug.Length > 80) slug = slug[..80].TrimEnd('-');
        return slug.Length == 0 ? "post" : slug;
    }

    public DomainError? Check()
    {
        Title = Title.Trim();
        if (Title.Length is 0 or > 200) return new("news.title", "Give it a title (up to 200 characters).", "title");
        if (string.IsNullOrWhiteSpace(Body)) return new("news.body", "Write the content.", "body");
        if (Audience == Audience.None) return new("news.audience", "Choose who sees it.", "audience");
        if (ShowFrom is { } from && ShowUntil is { } until && until < from) return new("news.window", "The end date must be on or after the start date.", "showUntil");
        if (LinkUrl is { } url && !(Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme is "https" or "http") && !url.StartsWith('/'))
            return new("news.link", "The link must be a web address (https://…) or a page on the site (/admissions).", "linkUrl");
        if (LinkUrl is not null && string.IsNullOrWhiteSpace(LinkText)) LinkText = "Learn more";
        if ((InCarousel || InTicker || IsFeatured) && !Audience.HasFlag(Audience.Public))
            return new("news.public", "Only public posts can appear on the website's carousel, ticker or pop-up.", "audience");
        return null;
    }
}

public class SchoolEvent : Entity
{
    public DomainError? Check()
    {
        Title = Title.Trim();
        if (Title.Length is 0 or > 200) return new("event.title", "Give the event a title (up to 200 characters).", "title");
        if (EndsAt is { } end && end < StartsAt) return new("event.end", "The event must end after it starts.", "endsAt");
        if (Audience == Audience.None) return new("event.audience", "Choose who sees it.", "audience");
        return null;
    }

    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public string? Location { get; set; }
    public Audience Audience { get; set; } = Audience.Everyone;
}

/// <summary>What a personal notification is about. Homework and the communication book (E17) will add kinds.</summary>
public enum NotificationKind
{
    PaymentReceived,
    FeesOverdue,
}

/// <summary>
/// A message for one signed-in user (E12.8), e.g. a guardian about their child's fees, shown under the portal bell.
/// <see cref="DedupeKey"/> stops one event notifying the same person twice (retries, both online payment paths).
/// </summary>
public class Notification : Entity
{
    public Guid UserId { get; set; }
    public NotificationKind Kind { get; set; }
    public required string Title { get; set; }
    public required string Body { get; set; }

    /// <summary>A portal path to open, e.g. <c>/portal/fees</c>.</summary>
    public string? LinkUrl { get; set; }
    public Guid? StudentId { get; set; }
    public string? DedupeKey { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}

/// <summary>That a signed-in user has read a notice (E12.1): drives the unread badge.</summary>
public class NoticeRead
{
    public Guid UserId { get; set; }
    public Guid AnnouncementId { get; set; }
    public DateTimeOffset ReadAt { get; set; }
}

/// <summary>A parent's or student's words for the website (E7.1), shown when published, in order.</summary>
public class Testimonial : Entity
{
    public required string Quote { get; set; }

    /// <summary>Who said it, without identifying a child, e.g. "Parent of a Year 3 pupil".</summary>
    public required string Attribution { get; set; }
    public int Order { get; set; }
    public bool IsPublished { get; set; } = true;
}

public class GalleryPhoto : Entity
{
    public required string Title { get; set; }
    public string? Caption { get; set; }
    public required string Category { get; set; }
    public required string ImagePath { get; set; }
    public int Order { get; set; }
    public bool IsFeatured { get; set; }
}
