using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt.RegenerateQuestions;

internal sealed class RegenerateQuestionsHandler(IAppDbContext db, AiQuestions ai, IQuestionGenerator generator, ICurrentUser me)
    : ICommandHandler<RegenerateQuestionsCommand, AiGenerationDto>
{
    public async Task<Result<AiGenerationDto>> Handle(RegenerateQuestionsCommand c, CancellationToken ct)
    {
        if (!generator.IsEnabled) return CbtErrors.AiUnavailable;
        var generation = await db.AiGenerations.FirstOrDefaultAsync(g => g.Id == c.Id && g.UserId == me.UserId, ct);
        if (generation is null) return CbtErrors.GenerationNotFound;
        if (!generation.CanRegenerate)
            return Error.Rule("ai.regenerations", $"You've regenerated this {Domain.Cbt.AiGeneration.MaxRegenerations} times. Pick a version, edit it, or start a new request.");
        if (await ai.CanSpendAsync(ct) is { } spent) return spent.ToError();
        await ai.RunAsync(generation, ct);
        await db.SaveChangesAsync(ct);
        return await ai.DtoAsync(generation, ct);
    }
}
