using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Maydel.Application.Abstractions;
using Maydel.Domain.Cbt;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Maydel.Infrastructure.Ai;

/// <summary>
/// The free fallback (E16.8): Google Gemini's generateContent REST API with the same instructions, PDFs/photos inline and a JSON
/// response schema. Used only when no Anthropic key is configured. Free-tier limits are per Google project (a few requests a minute).
/// </summary>
internal sealed class GeminiQuestionGenerator(IHttpClientFactory http, IOptions<AiOptions> options, ILogger<GeminiQuestionGenerator> logger)
    : IQuestionGenerator
{
    public const string HttpClientName = "gemini";

    public bool IsEnabled => !string.IsNullOrWhiteSpace(options.Value.GeminiApiKey);

    public async Task<GenerationResult> GenerateAsync(GenerationRequest r, CancellationToken ct)
    {
        if (!IsEnabled) return new GenerationResult(false, [], "AI question writing isn't set up on this server yet.", null, 0, 0);
        var model = options.Value.GeminiModel;
        var parts = new JsonArray();
        foreach (var f in r.Files)
            parts.Add(new JsonObject { ["inlineData"] = new JsonObject { ["mimeType"] = f.ContentType, ["data"] = Convert.ToBase64String(f.Content) } });
        parts.Add(new JsonObject { ["text"] = ClaudeQuestionGenerator.Prompt(r) });
        var body = new JsonObject
        {
            ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = ClaudeQuestionGenerator.System }) },
            ["contents"] = new JsonArray(new JsonObject { ["role"] = "user", ["parts"] = parts }),
            ["generationConfig"] = new JsonObject
            {
                ["responseMimeType"] = "application/json",
                ["responseSchema"] = Schema(),
                ["maxOutputTokens"] = 16000,
            },
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent")
            {
                Content = JsonContent.Create(body),
            };
            request.Headers.Add("x-goog-api-key", options.Value.GeminiApiKey);
            using var response = await http.CreateClient(HttpClientName).SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return new GenerationResult(false, [], "The free AI allowance is busy right now. Please try again in a minute.", model, 0, 0);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Gemini returned {Status}: {Body}", (int)response.StatusCode, Truncate(await response.Content.ReadAsStringAsync(ct)));
                return new GenerationResult(false, [], response.StatusCode == HttpStatusCode.BadRequest
                    ? "The AI couldn't read the material. Check the files (PDF up to 100 pages, clear photos)."
                    : "The AI service didn't respond properly. Please try again.", model, 0, 0);
            }

            var json = await response.Content.ReadFromJsonAsync<JsonObject>(ct);
            var usage = json?["usageMetadata"];
            long input = usage?["promptTokenCount"]?.GetValue<long>() ?? 0, output = usage?["candidatesTokenCount"]?.GetValue<long>() ?? 0;
            if (json?["promptFeedback"]?["blockReason"] is not null)
                return new GenerationResult(false, [], "The AI declined to write questions from this material. Try different notes or wording.", model, input, output);
            var candidate = json?["candidates"]?[0];
            var finish = candidate?["finishReason"]?.GetValue<string>();
            if (finish is "SAFETY" or "PROHIBITED_CONTENT" or "BLOCKLIST" or "RECITATION")
                return new GenerationResult(false, [], "The AI declined to write questions from this material. Try different notes or wording.", model, input, output);
            if (finish == "MAX_TOKENS")
                return new GenerationResult(false, [], "The answer was too long. Ask for fewer questions at a time.", model, input, output);
            var text = string.Concat(candidate?["content"]?["parts"]?.AsArray().Select(p => p?["text"]?.GetValue<string>() ?? "") ?? []);
            var parsed = JsonSerializer.Deserialize<GeneratedSet>(text, Application.Cbt.AiQuestions.Json);
            return new GenerationResult(true, parsed?.Questions ?? [], null, model, input, output);
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException or InvalidOperationException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Gemini question generation failed");
            return new GenerationResult(false, [], "The AI service didn't respond properly. Please try again.", model, 0, 0);
        }
    }

    private sealed record GeneratedSet(List<GeneratedQuestion> Questions);

    private static string Truncate(string s) => s.Length > 500 ? s[..500] : s;

    /// <summary>Gemini's response schema (OpenAPI subset): same shape as Claude's.</summary>
    private static JsonObject Schema()
    {
        static JsonArray Names<T>() where T : struct, Enum => new([.. Enum.GetNames<T>().Select(n => (JsonNode?)JsonValue.Create(n))]);
        var question = new JsonObject
        {
            ["type"] = "OBJECT",
            ["properties"] = new JsonObject
            {
                ["kind"] = new JsonObject { ["type"] = "STRING", ["enum"] = Names<QuestionKind>() },
                ["text"] = new JsonObject { ["type"] = "STRING" },
                ["options"] = new JsonObject
                {
                    ["type"] = "ARRAY",
                    ["items"] = new JsonObject
                    {
                        ["type"] = "OBJECT",
                        ["properties"] = new JsonObject { ["text"] = new JsonObject { ["type"] = "STRING" }, ["isCorrect"] = new JsonObject { ["type"] = "BOOLEAN" } },
                        ["required"] = new JsonArray("text", "isCorrect"),
                    },
                },
                ["trueFalseAnswer"] = new JsonObject { ["type"] = "BOOLEAN", ["nullable"] = true },
                ["acceptedAnswers"] = new JsonObject { ["type"] = "ARRAY", ["items"] = new JsonObject { ["type"] = "STRING" } },
                ["markingGuide"] = new JsonObject { ["type"] = "STRING", ["nullable"] = true },
                ["topic"] = new JsonObject { ["type"] = "STRING" },
                ["difficulty"] = new JsonObject { ["type"] = "STRING", ["enum"] = Names<Difficulty>() },
            },
            ["required"] = new JsonArray("kind", "text", "options", "acceptedAnswers", "topic", "difficulty"),
        };
        return new JsonObject
        {
            ["type"] = "OBJECT",
            ["properties"] = new JsonObject { ["questions"] = new JsonObject { ["type"] = "ARRAY", ["items"] = question } },
            ["required"] = new JsonArray("questions"),
        };
    }
}
