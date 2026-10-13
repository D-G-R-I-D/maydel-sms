using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Photos.GetStaffPhoto;

internal sealed class GetStaffPhotoHandler(IAppDbContext db, PhotoStore photos) : IQueryHandler<GetStaffPhotoQuery, PhotoContent>
{
    public async Task<Result<PhotoContent>> Handle(GetStaffPhotoQuery q, CancellationToken ct)
    {
        // Staff photos are for staff screens; the endpoint's Staff policy is the rule. Public website photos come with E4.3.
        var key = await db.Staff.AsNoTracking().Where(x => x.Id == q.StaffId).Select(x => x.PhotoPath).FirstOrDefaultAsync(ct);
        return await photos.OpenAsync(key, ct);
    }
}
