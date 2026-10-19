using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt.ListGenerations;

internal sealed class ListGenerationsHandler(IAppDbContext db, ICurrentUser me) : IQueryHandler<ListGenerationsQuery, IReadOnlyList<AiGenerationSummary>>
{
    public async Task<Result<IReadOnlyList<AiGenerationSummary>>> Handle(ListGenerationsQuery q, CancellationToken ct) =>
        await db.AiGenerations.AsNoTracking().Where(g => g.UserId == me.UserId).OrderByDescending(g => g.CreatedAt).Take(50)
            .Join(db.Subjects, g => g.SubjectId, s => s.Id, (g, s) => new { g, s })
            .Join(db.ClassLevels, x => x.g.ClassLevelId, l => l.Id, (x, l) => new AiGenerationSummary(x.g.Id, x.s.Name, l.Name, x.g.AssessmentType, x.g.Count,
                x.g.Versions.Count, x.g.CreatedAt))
            .ToListAsync(ct);
}
