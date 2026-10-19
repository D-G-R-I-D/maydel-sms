using Maydel.Domain.Assessment;
using Maydel.Domain.Common;

namespace Maydel.Domain.Cbt;

public enum TestStatus
{
    Draft,
    /// <summary>Locked and visible to the chosen classes inside its window.</summary>
    Published,
}

public enum SectionMode
{
    /// <summary>The teacher picks the questions; everyone gets the same ones.</summary>
    Fixed,
    /// <summary>Each student gets <see cref="TestSection.RandomCount"/> questions drawn from the bank (optionally by topic/difficulty).</summary>
    Random,
}

/// <summary>
/// An online test or exam (E16.2): sections of one question kind each, a sitting length, an open/close window, the arms that
/// sit it, and optionally the assessment component its score is scaled into (E16.2a).
/// </summary>
public class CbtTest : Entity
{
    public const int MaxSections = 10;
    public const int MaxQuestions = 200;

    public required string Title { get; set; }
    public string? Instructions { get; set; }
    public Guid SubjectId { get; set; }
    public Guid ClassLevelId { get; set; }
    public Guid TermId { get; set; }

    /// <summary>The gradebook column it feeds (e.g. CAT / 30); null for practice tests.</summary>
    public Guid? ComponentId { get; set; }
    public List<Guid> ClassRoomIds { get; set; } = [];

    public int DurationMinutes { get; set; } = 40;
    public DateTimeOffset OpensAt { get; set; }
    public DateTimeOffset ClosesAt { get; set; }
    public int MaxAttempts { get; set; } = 1;
    public bool ShuffleQuestions { get; set; } = true;
    public bool ShuffleOptions { get; set; } = true;

    /// <summary>
    /// Show students the computer-marked score as soon as they hand in (provisional while theory awaits the teacher). Off: nothing
    /// until results are released (E16.4).
    /// </summary>
    public bool ShowScoreOnSubmit { get; set; }

    public TestStatus Status { get; set; } = TestStatus.Draft;
    public DateTimeOffset? PublishedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }

    /// <summary>Scores visible to students and parents from here on (E16.4); marks are then final.</summary>
    public DateTimeOffset? ResultsReleasedAt { get; set; }
    public DateTimeOffset? GradebookPushedAt { get; set; }

    public List<TestSection> Sections { get; set; } = [];

    public int QuestionCount => Sections.Sum(s => s.QuestionCount);
    public decimal TotalMarks => Sections.Sum(s => s.Marks);

    /// <summary>A raw score scaled to the component's maximum, rounded like the report sheet (2 dp, halves up).</summary>
    public static decimal Scale(decimal score, decimal totalMarks, decimal componentMax) =>
        totalMarks <= 0 ? 0 : ReportMath.Round(score / totalMarks * componentMax);

    private static readonly DomainError Locked = new("test.locked", "Published tests can't be changed. Unpublish it first.");

    public DomainError? CheckSchedule()
    {
        if (string.IsNullOrWhiteSpace(Title) || Title.Length > 150) return new("test.title", "Give the test a title (up to 150 characters).", "title");
        if (DurationMinutes is < 5 or > 300) return new("test.duration", "The sitting must last between 5 minutes and 5 hours.", "durationMinutes");
        if (ClosesAt <= OpensAt) return new("test.window", "The test must close after it opens.", "closesAt");
        if ((ClosesAt - OpensAt).TotalMinutes < DurationMinutes)
            return new("test.window", "The window is shorter than the sitting. Close it later or shorten the test.", "closesAt");
        if (MaxAttempts is < 1 or > 5) return new("test.attempts", "Allow between 1 and 5 attempts.", "maxAttempts");
        return null;
    }

    public DomainError? CanEdit() => Status == TestStatus.Draft ? null : Locked;

    /// <summary>Checks the section shapes (counts, marks, modes); which questions exist is checked by the caller.</summary>
    public DomainError? CheckSections()
    {
        if (Sections.Count > MaxSections) return new("test.sections", $"Use at most {MaxSections} sections.", "sections");
        // Sections loaded from the database come back in storage order: sort by the saved order (stable, so a new list sent with
        // Order set to its position keeps it) before renumbering 0..n-1.
        Sections = [.. Sections.OrderBy(s => s.Order)];
        for (var i = 0; i < Sections.Count; i++)
        {
            var s = Sections[i];
            s.Order = i;
            s.Title = s.Title.Trim();
            if (s.Title.Length is 0 or > 100) return new("test.section_title", $"Give section {i + 1} a title (up to 100 characters).", "sections");
            if (s.MarksPerQuestion is <= 0 or > 100 || decimal.Round(s.MarksPerQuestion, 2) != s.MarksPerQuestion)
                return new("test.section_marks", $"Section {i + 1}: marks per question must be more than 0 and at most 100.", "sections");
            if (s.Mode == SectionMode.Fixed)
            {
                (s.RandomCount, s.Topic, s.Difficulty) = (null, null, null);
                if (s.QuestionIds.Distinct().Count() != s.QuestionIds.Count) return new("test.duplicate", $"Section {i + 1} has a question twice.", "sections");
            }
            else
            {
                s.QuestionIds = [];
                if (s.RandomCount is not (>= 1 and <= MaxQuestions)) return new("test.random_count", $"Section {i + 1}: draw between 1 and {MaxQuestions} questions.", "sections");
                s.Topic = string.IsNullOrWhiteSpace(s.Topic) ? null : s.Topic.Trim();
            }
        }
        if (Sections.SelectMany(s => s.QuestionIds).GroupBy(x => x).Any(g => g.Count() > 1))
            return new("test.duplicate", "A question appears in two sections.", "sections");
        if (QuestionCount > MaxQuestions) return new("test.too_many", $"Use at most {MaxQuestions} questions in one test.", "sections");
        return null;
    }

    public DomainError? Publish(DateTimeOffset now, IReadOnlyList<string> problems)
    {
        if (Status == TestStatus.Published) return new("test.published", "This test is already published.");
        if (problems.Count > 0) return new("test.not_ready", "The test isn't ready: " + problems[0]);
        if (ClosesAt <= now) return new("test.window", "The window has already closed. Move the closing time.", "closesAt");
        (Status, PublishedAt) = (TestStatus.Published, now);
        return null;
    }

    /// <summary>
    /// Releases the results after the window has closed, once nobody is still sitting and every answer has a mark (E16.4).
    /// </summary>
    public DomainError? ReleaseResults(DateTimeOffset now, IReadOnlyCollection<TestAttempt> attempts)
    {
        if (Status != TestStatus.Published) return new("test.draft", "This test isn't published.");
        if (ResultsReleasedAt is not null) return new("test.released", "The results are already released.");
        if (now < ClosesAt) return new("test.still_open", "Results can be released once the test has closed.");
        if (attempts.Count(a => a.Status == AttemptStatus.InProgress) is > 0 and var sitting)
            return new("test.still_sitting", $"{sitting} student(s) are still sitting the test.");
        if (attempts.Sum(a => a.NeedsMarking) is > 0 and var unmarked)
            return new("test.unmarked", $"{unmarked} answer(s) still need marking.");
        ResultsReleasedAt = now;
        return null;
    }

    /// <summary>Back to draft, only while nobody has started it (attempts arrive with E16.3).</summary>
    public DomainError? Unpublish(bool anyAttempts)
    {
        if (Status != TestStatus.Published) return new("test.draft", "This test isn't published.");
        if (anyAttempts) return new("test.started", "Students have already started this test, so it can't be unpublished.");
        (Status, PublishedAt) = (TestStatus.Draft, null);
        return null;
    }
}

public class TestSection
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public int Order { get; set; }
    public required string Title { get; set; }
    public string? Instructions { get; set; }
    public QuestionKind Kind { get; set; }
    public decimal MarksPerQuestion { get; set; } = 1;
    public SectionMode Mode { get; set; } = SectionMode.Fixed;

    /// <summary>Fixed mode: the questions, in order.</summary>
    public List<Guid> QuestionIds { get; set; } = [];

    /// <summary>Random mode: how many to draw per student, and optional filters.</summary>
    public int? RandomCount { get; set; }
    public string? Topic { get; set; }
    public Difficulty? Difficulty { get; set; }

    public int QuestionCount => Mode == SectionMode.Fixed ? QuestionIds.Count : RandomCount ?? 0;
    public decimal Marks => QuestionCount * MarksPerQuestion;
}
