using System.Collections.Concurrent;
using Maydel.Application.Abstractions;
using Maydel.Domain.Cbt;

namespace Maydel.Api.Tests.Infrastructure;

/// <summary>Writes predictable questions instead of calling Claude; records every request so tests can check what was sent.</summary>
public sealed class FakeQuestionGenerator : IQuestionGenerator
{
    public bool IsEnabled { get; set; } = true;
    public ConcurrentQueue<GenerationRequest> Requests { get; } = new();

    /// <summary>When set, the next call fails with this message (e.g. a refusal).</summary>
    public string? FailNextWith { get; set; }

    public Task<GenerationResult> GenerateAsync(GenerationRequest request, CancellationToken ct)
    {
        Requests.Enqueue(request);
        if (FailNextWith is { } failure)
        {
            FailNextWith = null;
            return Task.FromResult(new GenerationResult(false, [], failure, "fake", 100, 0));
        }
        var round = request.Avoid.Count / Math.Max(1, request.Count) + 1;
        var questions = Enumerable.Range(0, request.Count).Select(i =>
        {
            var kind = request.Kinds[i % request.Kinds.Count];
            var difficulty = i < request.Easy ? Difficulty.Easy : i < request.Easy + request.Medium ? Difficulty.Medium : Difficulty.Hard;
            var text = $"{request.Subject} v{round} q{i + 1}";
            return kind switch
            {
                QuestionKind.Objective => new GeneratedQuestion(kind, text, [new("Right", true), new("Wrong", false), new("Also wrong", false), new("No", false)],
                    null, [], null, "Topic", difficulty),
                QuestionKind.TrueFalse => new GeneratedQuestion(kind, text, [], true, [], null, "Topic", difficulty),
                QuestionKind.FillInTheGap => new GeneratedQuestion(kind, text + " ___.", [], null, ["answer"], null, "Topic", difficulty),
                QuestionKind.Theory => new GeneratedQuestion(kind, text, [], null, [], "Two points, 2 marks each.", "Topic", difficulty),
                _ => new GeneratedQuestion(kind, text, [new("Only", true)], null, ["x"], null, "Topic", difficulty), // deliberately wrong: one option
            };
        }).ToList();
        return Task.FromResult(new GenerationResult(true, questions, null, "fake", 1200, 800));
    }
}
