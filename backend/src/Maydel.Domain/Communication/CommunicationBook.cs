using Maydel.Domain.Common;
using Maydel.Domain.Homework;

namespace Maydel.Domain.Communication;

public enum BookSide
{
    /// <summary>The form teacher or an admin.</summary>
    School,

    /// <summary>A guardian or the student.</summary>
    Family,
}

/// <summary>
/// One note in a student's communication book (E17.2): a running notebook between the school (form teacher, admins)
/// and the family (guardians and the student). Notes can be corrected for 15 minutes, then only followed up.
/// </summary>
public class BookEntry : Entity
{
    public static readonly TimeSpan EditWindow = TimeSpan.FromMinutes(15);

    public Guid StudentId { get; set; }
    public Guid AuthorUserId { get; set; }
    public required string AuthorName { get; set; }
    public BookSide Side { get; set; }
    public required string Body { get; set; }
    public HomeworkFile? Attachment { get; set; }
    public DateTimeOffset? EditedAt { get; set; }

    public bool CanEdit(Guid userId, DateTimeOffset now) => AuthorUserId == userId && now - CreatedAt <= EditWindow;

    public static DomainError? CheckBody(string? body, bool hasFile)
    {
        var text = body?.Trim() ?? "";
        if (text.Length == 0 && !hasFile) return new("book.empty", "Write a note or attach a file.", "body");
        if (text.Length > 4000) return new("book.long", "Keep a note under 4,000 characters.", "body");
        return null;
    }
}

/// <summary>When a user was last sent the daily communication-book email (at most one a day).</summary>
public class BookDigestMark
{
    public Guid UserId { get; set; }
    public DateTimeOffset SentAt { get; set; }
}

/// <summary>Up to when a user has read a student's book: drives the unread counts.</summary>
public class BookRead
{
    public Guid StudentId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset ReadAt { get; set; }
}
