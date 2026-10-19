using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.SchoolSettings.GetPublicSchoolProfile;

internal sealed class GetPublicSchoolProfileHandler(IAppDbContext db) : IQueryHandler<GetPublicSchoolProfileQuery, PublicSchoolProfile>
{
    public async Task<Result<PublicSchoolProfile>> Handle(GetPublicSchoolProfileQuery q, CancellationToken ct) =>
        (await db.SchoolProfiles.AsNoTracking().OrderBy(p => p.CreatedAt).FirstOrDefaultAsync(ct)
            ?? new Domain.Settings.SchoolProfile { Name = "School", Address = "" }).ToPublic();
}
