using System.Security.Cryptography;
using Maydel.Domain.Common;

namespace Maydel.Domain.Cbt;

public enum AttemptStatus
{
    InProgress,
    /// <summary>Handed in (by the student or automatically at the deadline); objective parts are marked.</summary>
    Submitted,
}

public enum IntegrityEventKind
{
    /// <summary>The test tab lost focus (another tab or app).</summary>
    Blur,
    Focus,
    /// <summary>The attempt was opened on another device or tab, which took over.</summary>
    TakeOver,
}

/// <summary>
/// One student's sitting of a test (E16.3). The server's clock decides the deadline (the earlier of start + length and the
/// window's close). Only the device holding the current session token may save (E16.6).
/// </summary>
public class TestAttempt : Entity
{
    public Guid TestId { get; set; }
    public Guid StudentId { get; set; }
    public int Number { get; set; } = 1;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset DeadlineAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public bool AutoSubmitted { get; set; }
    public AttemptStatus Status { get; set; } = AttemptStatus.InProgress;
    public required string SessionToken { get; set; }

    public List<AttemptItem> Items { get; set; } = [];
    public List<IntegrityEvent> Events { get; set; } = [];

    /// <summary>Saves within this many seconds after the deadline still count (the last keystrokes in flight).</summary>
    public const int GraceSeconds = 15;

    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    /// <summary>Handed in and every question has a score (computer or teacher).</summary>
    public bool IsFullyMarked => Status == AttemptStatus.Submitted && Items.All(i => i.Score is not null);
    public decimal? TotalScore => IsFullyMarked ? Items.Sum(i => i.Score!.Value) : null;
    public int NeedsMarking => Status == AttemptStatus.Submitted ? Items.Count(i => i.Score is null) : 0;

    /// <summary>
    /// The teacher marks (or re-marks, overriding the computer) one answer (E16.4). Allowed until results are released.
    /// </summary>
    public DomainError? Mark(Guid itemId, decimal score, string? comment, Guid? markedBy, DateTimeOffset now)
    {
        if (Status != AttemptStatus.Submitted) return new("attempt.not_submitted", "This attempt hasn't been handed in yet.");
        var item = Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null) return new("attempt.item", "That question isn't in this attempt.", "itemId");
        if (score < 0 || score > item.Marks || decimal.Round(score, 2) != score)
            return new("attempt.score", $"Give between 0 and {item.Marks:0.##} marks (at most 2 decimal places).", "score");
        if (comment is { Length: > 1000 }) return new("attempt.comment", "Keep the comment under 1000 characters.", "comment");
        (item.TeacherScore, item.TeacherComment, item.MarkedAt, item.MarkedByUserId) =
            (score, string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(), now, markedBy);
        return null;
    }

    public bool IsOverdue(DateTimeOffset now) => Status == AttemptStatus.InProgress && now > DeadlineAt.AddSeconds(GraceSeconds);

    /// <summary>Opens the attempt on this device; any other device's token stops working.</summary>
    public string TakeOver(DateTimeOffset now)
    {
        if (Events.Count > 0 || Items.Any(i => i.AnsweredAt is not null)) Events.Add(new IntegrityEvent { Kind = IntegrityEventKind.TakeOver, At = now });
        SessionToken = NewToken();
        return SessionToken;
    }

    private DomainError? CheckOpen(string token, DateTimeOffset now)
    {
        if (Status != AttemptStatus.InProgress) return new("attempt.submitted", "This test has already been handed in.");
        if (now > DeadlineAt.AddSeconds(GraceSeconds)) return new("attempt.time_up", "Time is up. Your answers were handed in automatically.");
        if (!CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(token ?? ""), System.Text.Encoding.UTF8.GetBytes(SessionToken)))
            return new("attempt.other_session", "This test is open on another device or tab. Continue there, or reopen it here.");
        return null;
    }

    public DomainError? Answer(Guid itemId, QuestionAnswer answer, string token, DateTimeOffset now)
    {
        if (CheckOpen(token, now) is { } closed) return closed;
        var item = Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null) return new("attempt.item", "That question isn't in this test.", "itemId");
        if (answer.Text is { Length: > 5000 }) return new("attempt.too_long", "Keep the answer under 5000 characters.", "text");
        if (answer.OptionIds is { } ids && ids.Any(id => !item.OptionOrder.Contains(id))) return new("attempt.option", "That option isn't part of the question.", "optionIds");
        (item.AnswerOptionIds, item.AnswerTrueFalse, item.AnswerText, item.AnsweredAt) =
            (answer.OptionIds is { Count: > 0 } o ? [.. o.Distinct()] : [], answer.TrueFalse, string.IsNullOrWhiteSpace(answer.Text) ? null : answer.Text, now);
        return null;
    }

    public DomainError? Log(IntegrityEventKind kind, string token, DateTimeOffset now)
    {
        if (CheckOpen(token, now) is { } closed) return closed;
        if (Events.Count >= 500) return null; // enough evidence; don't let a flapping window grow the row forever
        Events.Add(new IntegrityEvent { Kind = kind, At = now });
        return null;
    }

    /// <summary>Hands the attempt in and marks what the computer can. <paramref name="questions"/>: the bank questions by id.</summary>
    public DomainError? Submit(string? token, DateTimeOffset now, IReadOnlyDictionary<Guid, Question> questions, bool automatic = false)
    {
        if (Status != AttemptStatus.InProgress) return new("attempt.submitted", "This test has already been handed in.");
        if (!automatic && CheckOpen(token ?? "", now) is { Code: "attempt.other_session" } other) return other;
        foreach (var item in Items)
        {
            var share = questions.TryGetValue(item.QuestionId, out var q) ? q.AutoMark(item.ToAnswer()) : null;
            item.AutoScore = share is { } s ? Math.Round(s * item.Marks, 2) : null;
        }
        (Status, SubmittedAt, AutoSubmitted) = (AttemptStatus.Submitted, automatic ? DeadlineAt : now, automatic);
        return null;
    }
}

/// <summary>A question as it appears in one student's attempt: its place, its marks, the option order they saw, and their answer.</summary>
public class AttemptItem
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public int Order { get; set; }
    public Guid SectionId { get; set; }
    public Guid QuestionId { get; set; }
    public decimal Marks { get; set; }
    public List<Guid> OptionOrder { get; set; } = [];

    public List<Guid> AnswerOptionIds { get; set; } = [];
    public bool? AnswerTrueFalse { get; set; }
    public string? AnswerText { get; set; }
    public DateTimeOffset? AnsweredAt { get; set; }

    /// <summary>Marks earned when the computer marks it; null for teacher-marked questions (E16.4).</summary>
    public decimal? AutoScore { get; set; }

    /// <summary>The teacher's mark: for theory, or to override the computer.</summary>
    public decimal? TeacherScore { get; set; }
    public string? TeacherComment { get; set; }
    public DateTimeOffset? MarkedAt { get; set; }
    public Guid? MarkedByUserId { get; set; }

    public decimal? Score => TeacherScore ?? AutoScore;

    public QuestionAnswer? ToAnswer() => AnsweredAt is null ? null : new QuestionAnswer(AnswerOptionIds, AnswerTrueFalse, AnswerText);
}

public class IntegrityEvent
{
    public IntegrityEventKind Kind { get; set; }
    public DateTimeOffset At { get; set; }
}

/// <summary>Builds a student's question paper: sections in order, questions (fixed or drawn) and options shuffled when the test says so.</summary>
public static class Paper
{
    public static List<AttemptItem> Build(CbtTest test, IReadOnlyDictionary<Guid, Question> fixedQuestions,
        Func<TestSection, IReadOnlyList<Question>> drawPool, Random random)
    {
        var items = new List<AttemptItem>();
        var used = new HashSet<Guid>(test.Sections.SelectMany(s => s.QuestionIds));
        foreach (var section in test.Sections.OrderBy(s => s.Order))
        {
            List<Question> chosen;
            if (section.Mode == SectionMode.Fixed)
            {
                chosen = [.. section.QuestionIds.Where(fixedQuestions.ContainsKey).Select(id => fixedQuestions[id])];
            }
            else
            {
                var pool = drawPool(section).Where(q => !used.Contains(q.Id)).ToArray();
                random.Shuffle(pool);
                chosen = [.. pool.Take(section.RandomCount ?? 0)];
                foreach (var q in chosen) used.Add(q.Id);
            }
            if (test.ShuffleQuestions)
            {
                var shuffled = chosen.ToArray();
                random.Shuffle(shuffled);
                chosen = [.. shuffled];
            }
            foreach (var q in chosen)
            {
                var options = q.Options.OrderBy(o => o.Order).Select(o => o.Id).ToArray();
                if (test.ShuffleOptions) random.Shuffle(options);
                items.Add(new AttemptItem { Order = items.Count, SectionId = section.Id, QuestionId = q.Id, Marks = section.MarksPerQuestion, OptionOrder = [.. options] });
            }
        }
        return items;
    }
}
