using System.Text.RegularExpressions;
using Maydel.Domain.Common;

namespace Maydel.Domain.Cbt;

public enum QuestionKind
{
    /// <summary>Multiple choice, exactly one correct option.</summary>
    Objective,
    /// <summary>Several correct options; full marks only for exactly the right set.</summary>
    MultiSelect,
    TrueFalse,
    /// <summary>A sentence with one gap ("___"); auto-marked against the accepted answers.</summary>
    FillInTheGap,
    /// <summary>A short typed answer: auto-marked when accepted answers are given, otherwise by the teacher.</summary>
    ShortAnswer,
    /// <summary>Essay/theory: marked by the teacher against the marking guide.</summary>
    Theory,
}

public enum Difficulty
{
    Easy,
    Medium,
    Hard,
}

public enum QuestionSource
{
    Manual,
    /// <summary>Generated with AI from the teacher's material (E16.8), then reviewed.</summary>
    Ai,
}

/// <summary>A student's answer to one question (E16.3). Only the part matching the question's kind is used.</summary>
public sealed record QuestionAnswer(IReadOnlyList<Guid>? OptionIds = null, bool? TrueFalse = null, string? Text = null);

/// <summary>A question in the bank (E16.1), for one subject and class level, shared by the teachers of that subject.</summary>
public partial class Question : Entity
{
    public const int MaxTextLength = 4000;
    public const int MaxOptions = 8;
    public const int MaxAcceptedAnswers = 10;

    public Guid SubjectId { get; set; }
    public Guid ClassLevelId { get; set; }
    public QuestionKind Kind { get; set; }
    public required string Text { get; set; }
    public string? ImagePath { get; set; }

    /// <summary>Objective and multi-select choices, in display order.</summary>
    public List<QuestionOption> Options { get; set; } = [];
    public bool? TrueFalseAnswer { get; set; }

    /// <summary>Fill-in-the-gap and short answer: any of these is correct (case and spacing don't matter).</summary>
    public List<string> AcceptedAnswers { get; set; } = [];

    /// <summary>Theory (required) and short answer (optional): what earns the marks.</summary>
    public string? MarkingGuide { get; set; }
    public string? Topic { get; set; }
    public Difficulty Difficulty { get; set; } = Difficulty.Medium;
    public QuestionSource Source { get; set; } = QuestionSource.Manual;
    public Guid? CreatedByUserId { get; set; }

    /// <summary>Removed from the bank; kept so tests that used it still show it.</summary>
    public bool IsArchived { get; set; }

    [GeneratedRegex("_{3,}")]
    private static partial Regex Gap();

    /// <summary>True when the computer can mark it (objective, multi-select, true/false, gap, or short answer with keys).</summary>
    public bool IsAutoMarked => Kind switch
    {
        QuestionKind.Theory => false,
        QuestionKind.ShortAnswer => AcceptedAnswers.Count > 0,
        _ => true,
    };

    /// <summary>
    /// Checks the question is complete for its kind and tidies it (drops fields other kinds use). Returns the first problem as a
    /// field error, or null.
    /// </summary>
    public DomainError? Normalise()
    {
        Text = Text.Trim();
        Topic = string.IsNullOrWhiteSpace(Topic) ? null : Topic.Trim();
        MarkingGuide = string.IsNullOrWhiteSpace(MarkingGuide) ? null : MarkingGuide.Trim();
        AcceptedAnswers = [.. AcceptedAnswers.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)];
        foreach (var o in Options) o.Text = o.Text.Trim();

        if (Text.Length == 0) return new("question.text", "Write the question.", "text");
        if (Text.Length > MaxTextLength) return new("question.text", $"Keep the question under {MaxTextLength} characters.", "text");

        switch (Kind)
        {
            case QuestionKind.Objective or QuestionKind.MultiSelect:
                (TrueFalseAnswer, AcceptedAnswers) = (null, []);
                if (Options.Count < 2 || Options.Count > MaxOptions)
                    return new("question.options", $"Give between 2 and {MaxOptions} options.", "options");
                if (Options.Any(o => o.Text.Length == 0)) return new("question.options", "Every option needs text.", "options");
                if (Options.Select(o => o.Text).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Options.Count)
                    return new("question.options", "Two options are the same.", "options");
                var correct = Options.Count(o => o.IsCorrect);
                if (Kind == QuestionKind.Objective && correct != 1) return new("question.answer", "Mark exactly one option as correct.", "options");
                if (Kind == QuestionKind.MultiSelect && correct < 1) return new("question.answer", "Mark at least one option as correct.", "options");
                for (var i = 0; i < Options.Count; i++) Options[i].Order = i;
                break;
            case QuestionKind.TrueFalse:
                (Options, AcceptedAnswers) = ([], []);
                if (TrueFalseAnswer is null) return new("question.answer", "Choose whether the statement is true or false.", "trueFalseAnswer");
                break;
            case QuestionKind.FillInTheGap:
                (Options, TrueFalseAnswer) = ([], null);
                if (Gap().Matches(Text).Count != 1) return new("question.gap", "Mark the one gap in the sentence with three underscores: ___", "text");
                if (AcceptedAnswers.Count == 0) return new("question.answer", "Give at least one accepted answer for the gap.", "acceptedAnswers");
                break;
            case QuestionKind.ShortAnswer:
                (Options, TrueFalseAnswer) = ([], null);
                break;
            case QuestionKind.Theory:
                (Options, TrueFalseAnswer, AcceptedAnswers) = ([], null, []);
                if (MarkingGuide is null) return new("question.guide", "Write the marking guide for this question.", "markingGuide");
                break;
            default:
                return new("question.kind", "Choose the kind of question.", "kind");
        }
        if (AcceptedAnswers.Count > MaxAcceptedAnswers || AcceptedAnswers.Any(a => a.Length > 200))
            return new("question.answer", $"Give up to {MaxAcceptedAnswers} accepted answers of up to 200 characters.", "acceptedAnswers");
        if (Options.Any(o => o.Text.Length > 1000)) return new("question.options", "Keep each option under 1000 characters.", "options");
        if (MarkingGuide is { Length: > MaxTextLength }) return new("question.guide", $"Keep the marking guide under {MaxTextLength} characters.", "markingGuide");
        if (Topic is { Length: > 80 }) return new("question.topic", "Keep the topic under 80 characters.", "topic");
        return null;
    }

    /// <summary>
    /// The share of the marks earned (0 or 1: no partial credit), or null when a teacher must mark it. A missing answer earns 0.
    /// </summary>
    public decimal? AutoMark(QuestionAnswer? answer)
    {
        if (!IsAutoMarked) return null;
        if (answer is null) return 0;
        var right = Kind switch
        {
            QuestionKind.Objective => answer.OptionIds is [var only] && Options.Any(o => o.Id == only && o.IsCorrect),
            QuestionKind.MultiSelect => answer.OptionIds is { Count: > 0 } ids
                && ids.ToHashSet().SetEquals(Options.Where(o => o.IsCorrect).Select(o => o.Id)),
            QuestionKind.TrueFalse => answer.TrueFalse is { } tf && tf == TrueFalseAnswer,
            _ => answer.Text is { } text && AcceptedAnswers.Any(a => Same(a, text)),
        };
        return right ? 1 : 0;
    }

    /// <summary>Case-, accent-spacing- and trailing-punctuation-insensitive comparison for typed answers.</summary>
    public static bool Same(string expected, string given)
    {
        static string N(string s) => string.Join(' ', s.Trim().TrimEnd('.', '!', '?', ';', ',').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
        return N(expected) == N(given);
    }
}

public class QuestionOption
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Text { get; set; }
    public bool IsCorrect { get; set; }
    public int Order { get; set; }
}
