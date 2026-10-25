using Maydel.Domain.Common;

namespace Maydel.Domain.Homework;

/// <summary>A file attached to homework or a hand-in, kept privately and only served to people who may see it.</summary>
public class HomeworkFile
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Key { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long Size { get; set; }
}

/// <summary>Homework a teacher sets for a class (and subject), due on a day (E17.1).</summary>
public class HomeworkTask : Entity
{
    public const int MaxFiles = 5;

    public Guid ClassRoomId { get; set; }

    /// <summary>Null = general homework from the form teacher.</summary>
    public Guid? SubjectId { get; set; }
    public Guid SetByUserId { get; set; }
    public required string SetByName { get; set; }
    public required string Title { get; set; }

    /// <summary>Plain text; derived from <see cref="InstructionsHtml"/> when written in the editor.</summary>
    public string Instructions { get; set; } = "";
    public string? InstructionsHtml { get; set; }
    public DateOnly DueOn { get; set; }

    /// <summary>Off for homework that's done on paper: students just see it.</summary>
    public bool AcceptsSubmissions { get; set; } = true;
    public List<HomeworkFile> Files { get; set; } = [];

    public DomainError? Check()
    {
        Title = Title.Trim();
        if (Title.Length is 0 or > 200) return new("homework.title", "Give the homework a title (up to 200 characters).", "title");
        if (Instructions.Length > 20000) return new("homework.instructions", "Keep the instructions under 20,000 characters.", "instructions");
        return null;
    }
}

public enum SubmissionStatus
{
    /// <summary>Handed in, waiting for the teacher.</summary>
    Submitted,

    /// <summary>The teacher has looked at it.</summary>
    Seen,

    /// <summary>Sent back with feedback (the student may hand in again).</summary>
    Returned,
}

/// <summary>A student's hand-in: text and/or files. One per student per homework; handing in again replaces it.</summary>
public class HomeworkSubmission : Entity
{
    public const int MaxFiles = 5;

    public Guid HomeworkId { get; set; }
    public Guid StudentId { get; set; }
    public string? Text { get; set; }
    public List<HomeworkFile> Files { get; set; } = [];
    public DateTimeOffset SubmittedAt { get; set; }
    public bool IsLate { get; set; }
    public SubmissionStatus Status { get; set; }
    public string? Feedback { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
}
