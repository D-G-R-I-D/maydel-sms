using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Students.GetGuardian;

internal sealed class GetGuardianHandler(IAppDbContext db) : IQueryHandler<GetGuardianQuery, GuardianDetail>
{
    public async Task<Result<GuardianDetail>> Handle(GetGuardianQuery query, CancellationToken ct) =>
        await db.GetGuardianDetailAsync(query.Id, ct) is { } guardian ? guardian : StudentErrors.GuardianNotFound;
}
