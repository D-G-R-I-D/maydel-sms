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

/// <summary>News / notices. Public ones also appear on the website's news section.</summary>
public class Announcement : Entity
{
    public required string Title { get; set; }
    public required string Body { get; set; }
    public string? Category { get; set; }
    public string? ImagePath { get; set; }
    public Audience Audience { get; set; } = Audience.Everyone;
    public bool IsPinned { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public Guid? AuthorUserId { get; set; }
}

public class SchoolEvent : Entity
{
    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public string? Location { get; set; }
    public Audience Audience { get; set; } = Audience.Everyone;
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
