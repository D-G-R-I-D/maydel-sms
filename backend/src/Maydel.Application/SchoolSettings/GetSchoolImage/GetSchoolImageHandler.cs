using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Photos;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.SchoolSettings.GetSchoolImage;

internal sealed class GetSchoolImageHandler(IAppDbContext db, PhotoStore photos) : IQueryHandler<GetSchoolImageQuery, PhotoContent>
{
    public async Task<Result<PhotoContent>> Handle(GetSchoolImageQuery q, CancellationToken ct)
    {
        var profile = await db.SchoolProfiles.AsNoTracking().OrderBy(p => p.CreatedAt).FirstOrDefaultAsync(ct);
        return await photos.OpenAsync(profile?.PathOf(q.Image), ct);
    }
}
