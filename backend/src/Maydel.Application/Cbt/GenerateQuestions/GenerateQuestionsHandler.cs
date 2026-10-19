using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Finance;
using Maydel.Domain.Cbt;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt.GenerateQuestions;

internal sealed class GenerateQuestionsHandler(IAppDbContext db, AccessRules access, AiQuestions ai, IQuestionGenerator generator, PaymentProofs files, ICurrentUser me)
    : ICommandHandler<GenerateQuestionsCommand, AiGenerationDto>
{
    public async Task<Result<AiGenerationDto>> Handle(GenerateQuestionsCommand c, CancellationToken ct)
    {
        if (!generator.IsEnabled) return CbtErrors.AiUnavailable;
        if (!await db.Subjects.AnyAsync(s => s.Id == c.SubjectId, ct) || !await db.ClassLevels.AnyAsync(l => l.Id == c.ClassLevelId, ct)) return CbtErrors.SubjectOrLevel;
        if (!await access.CanAuthorAsync(c.ClassLevelId, c.SubjectId, ct)) return CbtErrors.NotYourSubject;

        var generation = new AiGeneration
        {
            UserId = me.UserId!.Value, SubjectId = c.SubjectId, ClassLevelId = c.ClassLevelId, TermId = c.TermId, AssessmentType = c.AssessmentType,
            Kinds = [.. c.Kinds ?? []], Count = c.Count, Easy = c.Easy, Medium = c.Medium, Hard = c.Hard,
            Guidance = string.IsNullOrWhiteSpace(c.Guidance) ? null : c.Guidance.Trim(),
            Files = [.. (c.Files ?? []).Select(f => new AiSourceFile { Name = f.Name, FilePath = "", ContentType = "" })],
        };
        if (!Enum.IsDefined(c.AssessmentType)) return Error.Validation("assessmentType", "Choose test, CAT or exam.");
        if (generation.Check() is { } invalid) return invalid.ToError();
        if (await ai.CanSpendAsync(ct) is { } spent) return spent.ToError();

        // Store the material privately: photos are re-encoded (EXIF stripped), PDFs signature-checked.
        generation.Files.Clear();
        foreach (var f in c.Files ?? [])
        {
            if (await files.SaveAsync(f.Content, ct, folder: "ai-sources") is not { } saved)
                return Error.Validation("files", $"\"{f.Name}\" isn't a photo (JPEG, PNG, WebP up to 12 MB) or a PDF (up to 5 MB).");
            generation.Files.Add(new AiSourceFile { Name = Path.GetFileName(f.Name).Length > 200 ? Path.GetFileName(f.Name)[..200] : Path.GetFileName(f.Name),
                FilePath = saved.Key, ContentType = saved.ContentType });
        }

        db.AiGenerations.Add(generation);
        await ai.RunAsync(generation, ct);
        await db.SaveChangesAsync(ct);
        return await ai.DtoAsync(generation, ct);
    }
}
