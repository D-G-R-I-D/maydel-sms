using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.SchoolSettings.GetSchoolProfile;

internal sealed class GetSchoolProfileHandler(IAppDbContext db) : IQueryHandler<GetSchoolProfileQuery, SchoolProfileDto>
{
    public async Task<Result<SchoolProfileDto>> Handle(GetSchoolProfileQuery query, CancellationToken ct) =>
        (await db.CurrentAsync(ct)).ToDto();
}
