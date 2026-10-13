using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Academics.ListSessions;

internal sealed class ListSessionsHandler(IAppDbContext db) : IQueryHandler<ListSessionsQuery, IReadOnlyList<SessionDto>>
{
    public async Task<Result<IReadOnlyList<SessionDto>>> Handle(ListSessionsQuery query, CancellationToken ct) =>
        Result<IReadOnlyList<SessionDto>>.Success(await db.ListSessionsAsync(null, ct));
}
