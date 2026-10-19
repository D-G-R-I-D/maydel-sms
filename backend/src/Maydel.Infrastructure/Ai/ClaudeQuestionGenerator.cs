using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using Maydel.Application.Abstractions;
using Maydel.Domain.Cbt;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Maydel.Infrastructure.Ai;

public sealed class AiOptions
{
    public const string Section = "Ai";

    /// <summary>The Anthropic API key: user-secrets or the <c>Ai__ApiKey</c> environment variable, never appsettings.</summary>
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "claude-opus-5-5";
}

/// <summary>
/// Writes draft questions with Claude (E16.8) from the teacher's files and scope, as schema-checked JSON. Refusals and API errors
/// come back as a failed result in plain words; nothing here throws for an expected failure.
/// </summary>
internal sealed class ClaudeQuestionGenerator(IOptions<AiOptions> options, ILogger<ClaudeQuestionGenerator> logger) : IQuestionGenerator
{
    private readonly Lazy<AnthropicClient?> _client = new(() =>
        string.IsNullOrWhiteSpace(options.Value.ApiKey) ? null : new AnthropicClient { ApiKey = options.Value.ApiKey });

    public bool IsEnabled => !string.IsNullOrWhiteSpace(options.Value.ApiKey);

    private const string System = """
        You write assessment questions for a Nigerian primary and secondary school (British/Nigerian curriculum, Nigerian English).
        Write questions ONLY from the teaching material and topics the teacher provides, for the stated subject and class. Do not add
        topics that are not in the material. Questions must be accurate, unambiguous, age-appropriate and culturally relevant.

        Rules per kind:
        - Objective: 4 options, exactly one correct; plausible distractors.
        - MultiSelect: 4-6 options, two or more correct.
        - TrueFalse: a clear statement; set trueFalseAnswer.
        - FillInTheGap: one sentence with exactly one gap written as ___ ; list every acceptable answer in acceptedAnswers.
        - ShortAnswer: a question with a short answer; list acceptable answers in acceptedAnswers.
        - Theory: an open question; markingGuide says what earns each mark.
        Fields that don't apply to a kind are empty lists or null. Give each question a short topic taken from the material, and a
        difficulty of Easy, Medium or Hard matching the requested mix.
        """;

    public async Task<GenerationResult> GenerateAsync(GenerationRequest r, CancellationToken ct)
    {
        if (_client.Value is not { } client) return new GenerationResult(false, [], "AI question writing isn't set up on this server yet.", null, 0, 0);

        var content = new List<BetaContentBlockParam>();
        foreach (var f in r.Files)
        {
            var data = Convert.ToBase64String(f.Content);
            if (f.ContentType == "application/pdf")
                content.Add(new BetaRequestDocumentBlock { Source = new BetaBase64PdfSource { Data = data }, Title = f.Name });
            else
                content.Add(new BetaImageBlockParam { Source = new BetaBase64ImageSource { Data = data, MediaType = MediaType.ImageJpeg } });
        }
        content.Add(new BetaTextBlockParam { Text = Prompt(r) });

        try
        {
            var response = await client.Beta.Messages.Create(new MessageCreateParams
            {
                Model = options.Value.Model,
                MaxTokens = 16000,
                System = System,
                // A safety decline is re-served by the fallback model inside the same call.
                Betas = ["server-side-fallback-2026-06-01"],
                Fallbacks = new List<BetaFallbackParam> { new() { Model = "claude-opus-4-8" } },
                OutputConfig = new BetaOutputConfig { Effort = Effort.High, Format = new BetaJsonOutputFormat { Schema = Schema() } },
                Messages = [new BetaMessageParam { Role = Role.User, Content = content }],
            }, cancellationToken: ct);

            var usage = (response.Usage.InputTokens, response.Usage.OutputTokens);
            if (response.StopReason == "refusal")
                return new GenerationResult(false, [], "The AI declined to write questions from this material. Try different notes or wording.",
                    response.Model, usage.InputTokens, usage.OutputTokens);
            var text = string.Concat(response.Content.Select(b => b.TryPickText(out var t) ? t.Text : ""));
            if (response.StopReason == "max_tokens")
                return new GenerationResult(false, [], "The answer was too long. Ask for fewer questions at a time.", response.Model, usage.InputTokens, usage.OutputTokens);
            var parsed = JsonSerializer.Deserialize<GeneratedSet>(text, Application.Cbt.AiQuestions.Json);
            return new GenerationResult(true, parsed?.Questions ?? [], null, response.Model, usage.InputTokens, usage.OutputTokens);
        }
        catch (AnthropicRateLimitException ex)
        {
            logger.LogWarning(ex, "AI rate limited");
            return new GenerationResult(false, [], "The AI service is busy. Please try again in a minute.", null, 0, 0);
        }
        catch (AnthropicBadRequestException ex)
        {
            logger.LogWarning(ex, "AI rejected the request");
            return new GenerationResult(false, [], "The AI couldn't read the material. Check the files (PDF up to 100 pages, clear photos).", null, 0, 0);
        }
        catch (Exception ex) when (ex is AnthropicException or JsonException or HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "AI question generation failed");
            return new GenerationResult(false, [], "The AI service didn't respond properly. Please try again.", null, 0, 0);
        }
    }

    private sealed record GeneratedSet(List<GeneratedQuestion> Questions);

    private static string Prompt(GenerationRequest r)
    {
        var assessment = r.AssessmentType switch { AssessmentType.Cat => "continuous assessment test (CAT)", AssessmentType.Exam => "end-of-term examination", _ => "class test" };
        var avoid = r.Avoid.Count == 0 ? "" : "\n\nThe teacher has already seen these questions; write different ones:\n" + string.Join("\n", r.Avoid.Select(a => "- " + a));
        return $"""
            Subject: {r.Subject}
            Class: {r.ClassLevel}
            Assessment: {assessment}
            Write exactly {r.Count} questions using only these kinds: {string.Join(", ", r.Kinds)}.
            Difficulty mix: {r.Easy} Easy, {r.Medium} Medium, {r.Hard} Hard.
            {(string.IsNullOrWhiteSpace(r.Guidance) ? "" : "Teacher's guidance: " + r.Guidance)}
            Use only the attached material{(r.Files.Count == 0 ? " described in the guidance" : "")}.{avoid}
            """;
    }

    private static Dictionary<string, JsonElement> Schema()
    {
        var kinds = Enum.GetNames<QuestionKind>();
        var difficulties = Enum.GetNames<Difficulty>();
        var question = new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "kind", "text", "options", "trueFalseAnswer", "acceptedAnswers", "markingGuide", "topic", "difficulty" },
            properties = new Dictionary<string, object>
            {
                ["kind"] = new { type = "string", @enum = kinds },
                ["text"] = new { type = "string" },
                ["options"] = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object", additionalProperties = false, required = new[] { "text", "isCorrect" },
                        properties = new { text = new { type = "string" }, isCorrect = new { type = "boolean" } },
                    },
                },
                ["trueFalseAnswer"] = new { type = new[] { "boolean", "null" } },
                ["acceptedAnswers"] = new { type = "array", items = new { type = "string" } },
                ["markingGuide"] = new { type = new[] { "string", "null" } },
                ["topic"] = new { type = "string" },
                ["difficulty"] = new { type = "string", @enum = difficulties },
            },
        };
        return new Dictionary<string, JsonElement>
        {
            ["type"] = JsonSerializer.SerializeToElement("object"),
            ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
            ["required"] = JsonSerializer.SerializeToElement(new[] { "questions" }),
            ["properties"] = JsonSerializer.SerializeToElement(new { questions = new { type = "array", items = question } }),
        };
    }
}
