using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.StaffMembers.GetStaff;

internal sealed class GetStaffHandler(IAppDbContext db) : IQueryHandler<GetStaffQuery, StaffDetail>
{
    public async Task<Result<StaffDetail>> Handle(GetStaffQuery query, CancellationToken ct) =>
        await db.GetStaffDetailAsync(query.Id, ct) is { } staff ? staff : StaffErrors.NotFound;
}
