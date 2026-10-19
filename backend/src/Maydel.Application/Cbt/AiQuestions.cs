using System.Text.Json;
using Maydel.Application.Abstractions;
using Maydel.Application.Common.Access;
using Maydel.Domain.Cbt;
using Maydel.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt;

/// <summary>AI question writing (E16.8/E16.9): quota, running a version, and the views.</summary>
public sealed class AiQuestions(IAppDbContext db, IQuestionGenerator generator, IFileStorage storage, ICurrentUser me, ISchoolClock clock)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public async Task<AiSettings> SettingsAsync(CancellationToken ct) =>
        await db.AiSettings.FirstOrDefaultAsync(ct) ?? new AiSettings();

    /// <summary>Successful versions in the month: what the allowance counts.</summary>
    private IQueryable<AiVersion> Spent(DateTimeOffset from, DateTimeOffset to, Guid? userId = null) =>
        db.AiGenerations.Where(g => userId == null || g.UserId == userId).SelectMany(g => g.Versions)
            .Where(v => v.Status == GenerationStatus.Ready && v.CreatedAt >= from && v.CreatedAt < to);

    public async Task<AiQuotaDto> QuotaAsync(Guid userId, CancellationToken ct)
    {
        var settings = await SettingsAsync(ct);
        var (from, to) = AiQuota.Month(clock.Now);
        var allowance = await db.AiAllowances.Where(a => a.UserId == userId).Select(a => (int?)a.MonthlyGenerations).FirstOrDefaultAsync(ct)
            ?? settings.DefaultMonthlyGenerations;
        var used = await Spent(from, to, userId).CountAsync(ct);
        var schoolUsed = await Spent(from, to).CountAsync(ct);
        return new AiQuotaDto(generator.IsEnabled, settings.Enabled, allowance, used, Math.Max(0, allowance - used), settings.SchoolMonthlyGenerations,
            schoolUsed, to);
    }

    public async Task<DomainError?> CanSpendAsync(CancellationToken ct)
    {
        var q = await QuotaAsync(me.UserId!.Value, ct);
        return AiQuota.CanSpend(q.Allowance, q.Used, q.SchoolCap, q.SchoolUsed, q.Enabled);
    }

    /// <summary>Calls the AI for a new version (first or regenerated) and records it, succeeded or not.</summary>
    public async Task<AiVersion> RunAsync(AiGeneration g, CancellationToken ct)
    {
        var subject = await db.Subjects.Where(s => s.Id == g.SubjectId).Select(s => s.Name).FirstAsync(ct);
        var level = await db.ClassLevels.Where(l => l.Id == g.ClassLevelId).Select(l => l.Name).FirstAsync(ct);
        var files = new List<AiSource>();
        foreach (var f in g.Files)
        {
            if (await storage.OpenAsync(f.FilePath, ct) is not { } stream) continue;
            await using (stream)
            {
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, ct);
                files.Add(new AiSource(f.Name, f.ContentType, buffer.ToArray()));
            }
        }
        // Regenerating: steer away from what the teacher already saw.
        var avoid = g.Versions.Where(v => v.Status == GenerationStatus.Ready)
            .SelectMany(v => JsonSerializer.Deserialize<List<GeneratedQuestion>>(v.QuestionsJson, Json) ?? []).Select(q => q.Text).Take(200).ToList();

        var result = await generator.GenerateAsync(new GenerationRequest(subject, level, g.AssessmentType, g.Kinds, g.Count, g.Easy, g.Medium, g.Hard,
            g.Guidance, files, avoid), ct);
        var version = new AiVersion
        {
            Number = g.Versions.Count + 1, CreatedAt = clock.Now.ToUniversalTime(),
            Status = result.Succeeded && result.Questions.Count > 0 ? GenerationStatus.Ready : GenerationStatus.Failed,
            Message = result.Succeeded && result.Questions.Count == 0 ? "The AI didn't return any questions. Try adding more material." : result.Message,
            QuestionsJson = JsonSerializer.Serialize(result.Questions, Json), Model = result.Model,
            InputTokens = result.InputTokens, OutputTokens = result.OutputTokens,
        };
        g.Versions.Add(version);
        return version;
    }

    /// <summary>Checks a generated question as if it were going into the bank; returns the first problem in plain words.</summary>
    public static string? Problem(GeneratedQuestion q)
    {
        var probe = new Question
        {
            Kind = q.Kind, Text = q.Text ?? "", TrueFalseAnswer = q.TrueFalseAnswer, AcceptedAnswers = [.. q.AcceptedAnswers ?? []], MarkingGuide = q.MarkingGuide,
            Topic = q.Topic, Options = [.. (q.Options ?? []).Select(o => new QuestionOption { Text = o.Text ?? "", IsCorrect = o.IsCorrect })],
        };
        return probe.Normalise()?.Message;
    }

    public async Task<AiGenerationDto> DtoAsync(AiGeneration g, CancellationToken ct)
    {
        var subject = await db.Subjects.Where(s => s.Id == g.SubjectId).Select(s => s.Name).FirstAsync(ct);
        var level = await db.ClassLevels.Where(l => l.Id == g.ClassLevelId).Select(l => l.Name).FirstAsync(ct);
        var versions = g.Versions.OrderBy(v => v.Number).Select(v => new AiVersionDto(v.Id, v.Number, v.CreatedAt, v.Status, v.Message,
            [.. (JsonSerializer.Deserialize<List<GeneratedQuestion>>(v.QuestionsJson, Json) ?? []).Select((q, i) => new DraftQuestion(i, q, Problem(q)))])).ToList();
        return new AiGenerationDto(g.Id, g.SubjectId, subject, g.ClassLevelId, level, g.AssessmentType, g.Kinds, g.Count, g.Easy, g.Medium, g.Hard, g.Guidance,
            [.. g.Files.Select(f => f.Name)], versions, g.Regenerations, g.CanRegenerate, g.CreatedAt, await QuotaAsync(g.UserId, ct));
    }
}
