using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Academics.ListSubjects;

internal sealed class ListSubjectsHandler(IAppDbContext db) : IQueryHandler<ListSubjectsQuery, IReadOnlyList<SubjectDto>>
{
    public async Task<Result<IReadOnlyList<SubjectDto>>> Handle(ListSubjectsQuery query, CancellationToken ct) =>
        await db.Subjects.AsNoTracking().OrderBy(s => s.Name).Select(s => new SubjectDto(s.Id, s.Name, s.Code, s.IsActive)).ToListAsync(ct);
}
