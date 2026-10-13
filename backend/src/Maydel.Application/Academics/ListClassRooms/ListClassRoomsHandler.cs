using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Academics.ListClassRooms;

internal sealed class ListClassRoomsHandler(IAppDbContext db) : IQueryHandler<ListClassRoomsQuery, IReadOnlyList<ClassRoomDto>>
{
    public async Task<Result<IReadOnlyList<ClassRoomDto>>> Handle(ListClassRoomsQuery query, CancellationToken ct) =>
        await db.ClassRoomDtos().ToListAsync(ct);
}
