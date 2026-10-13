using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Academics.ListClassLevels;

internal sealed class ListClassLevelsHandler(IAppDbContext db) : IQueryHandler<ListClassLevelsQuery, IReadOnlyList<ClassLevelDto>>
{
    public async Task<Result<IReadOnlyList<ClassLevelDto>>> Handle(ListClassLevelsQuery query, CancellationToken ct) =>
        await db.ClassLevels.AsNoTracking().OrderBy(l => l.Order)
            .Select(l => new ClassLevelDto(l.Id, l.Name, l.Section, l.Order)).ToListAsync(ct);
}
