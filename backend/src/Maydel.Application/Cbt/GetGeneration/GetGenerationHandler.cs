using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt.GetGeneration;

internal sealed class GetGenerationHandler(IAppDbContext db, AiQuestions ai, ICurrentUser me) : IQueryHandler<GetGenerationQuery, AiGenerationDto>
{
    public async Task<Result<AiGenerationDto>> Handle(GetGenerationQuery q, CancellationToken ct) =>
        await db.AiGenerations.AsNoTracking().FirstOrDefaultAsync(g => g.Id == q.Id && g.UserId == me.UserId, ct) is { } g
            ? await ai.DtoAsync(g, ct)
            : CbtErrors.GenerationNotFound;
}
