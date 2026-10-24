using Maydel.Domain.Academics;
using Maydel.Domain.Common;

namespace Maydel.Domain.Communication;

/// <summary>Who a newsletter goes to; combine as needed.</summary>
[Flags]
public enum NewsletterAudience
{
    None = 0,
    Parents = 1,
    Staff = 2,
    Subscribers = 4,
}

public enum NewsletterStatus
{
    Draft,
    Scheduled,
    Sent,
}

/// <summary>
/// A newsletter (E12.6): written in the editor, sent in the school's branded email layout to parents (optionally one
/// section or class), staff and confirmed website subscribers, and optionally kept on the website as an archive page.
/// </summary>
public class Newsletter : Entity
{
    /// <summary>The email subject and the page title.</summary>
    public required string Title { get; set; }

    /// <summary>The short line inboxes show after the subject.</summary>
    public string? Preheader { get; set; }
    public required string BodyHtml { get; set; }
    public required string BodyText { get; set; }
    public NewsletterAudience Audience { get; set; } = NewsletterAudience.Parents;

    /// <summary>Parents of this section only (null = every section).</summary>
    public SchoolSection? Section { get; set; }

    /// <summary>Parents of this class only (null = every class).</summary>
    public Guid? ClassRoomId { get; set; }
    public NewsletterStatus Status { get; set; }
    public DateTimeOffset? ScheduledFor { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public int Recipients { get; set; }
    public bool PublishOnWebsite { get; set; }
    public string Slug { get; set; } = "";

    public bool IsEditable => Status == NewsletterStatus.Draft;

    public DomainError? Check()
    {
        Title = Title.Trim();
        if (Title.Length is 0 or > 150) return new("newsletter.title", "Give it a subject (up to 150 characters).", "title");
        if (Preheader is { Length: > 200 }) return new("newsletter.preheader", "Keep the preview line under 200 characters.", "preheader");
        if (string.IsNullOrWhiteSpace(BodyText) && !BodyHtml.Contains("<img", StringComparison.OrdinalIgnoreCase))
            return new("newsletter.body", "Write the newsletter.", "bodyHtml");
        if (Audience == NewsletterAudience.None) return new("newsletter.audience", "Choose who receives it.", "audience");
        if ((Section is not null || ClassRoomId is not null) && !Audience.HasFlag(NewsletterAudience.Parents))
            return new("newsletter.audience", "A section or class only narrows the parents; include parents.", "audience");
        return null;
    }
}

/// <summary>An address that unsubscribed from newsletters. School notices (fees, results) still reach it.</summary>
public class NewsletterOptOut
{
    public required string Email { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
