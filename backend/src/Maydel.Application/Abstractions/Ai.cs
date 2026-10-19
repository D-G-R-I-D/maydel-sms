using Maydel.Domain.Cbt;

namespace Maydel.Application.Abstractions;

/// <summary>A file the teacher uploaded: a PDF, or a photo already re-encoded to JPEG.</summary>
public sealed record AiSource(string Name, string ContentType, byte[] Content);

/// <summary>
/// What to write. Only teaching material and scope go to the AI: subject, class and assessment names, the teacher's files and
/// guidance, and (when regenerating) the previous questions to avoid. Never student data (E16.8).
/// </summary>
public sealed record GenerationRequest(
    string Subject, string ClassLevel, AssessmentType AssessmentType, IReadOnlyList<QuestionKind> Kinds, int Count, int Easy, int Medium, int Hard,
    string? Guidance, IReadOnlyList<AiSource> Files, IReadOnlyList<string> Avoid);

public sealed record GeneratedOption(string Text, bool IsCorrect);

/// <summary>A draft question with its answer key; the teacher reviews and edits before it reaches the bank.</summary>
public sealed record GeneratedQuestion(
    QuestionKind Kind, string Text, IReadOnlyList<GeneratedOption> Options, bool? TrueFalseAnswer, IReadOnlyList<string> AcceptedAnswers,
    string? MarkingGuide, string? Topic, Difficulty Difficulty);

/// <summary><c>Refused</c>/<c>Failed</c>: nothing usable came back; <c>Message</c> says why in plain words.</summary>
public sealed record GenerationResult(bool Succeeded, IReadOnlyList<GeneratedQuestion> Questions, string? Message, string? Model, long InputTokens, long OutputTokens);

public interface IQuestionGenerator
{
    /// <summary>False when no API key is configured: the feature shows as unavailable.</summary>
    bool IsEnabled { get; }

    Task<GenerationResult> GenerateAsync(GenerationRequest request, CancellationToken ct);
}
