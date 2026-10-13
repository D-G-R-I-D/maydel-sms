using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Academics.ListClassSubjects;

internal sealed class ListClassSubjectsHandler(IAppDbContext db) : IQueryHandler<ListClassSubjectsQuery, IReadOnlyList<ClassSubjectDto>>
{
    public async Task<Result<IReadOnlyList<ClassSubjectDto>>> Handle(ListClassSubjectsQuery query, CancellationToken ct)
    {
        if (!await db.ClassRooms.AnyAsync(r => r.Id == query.ClassRoomId, ct)) return AcademicsErrors.ClassRoomNotFound;
        return await db.ClassSubjectDtos(query.ClassRoomId).ToListAsync(ct);
    }
}
