using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Common.Access;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Photos.GetStudentPhoto;

internal sealed class GetStudentPhotoHandler(IAppDbContext db, PhotoStore photos, AccessRules access) : IQueryHandler<GetStudentPhotoQuery, PhotoContent>
{
    public async Task<Result<PhotoContent>> Handle(GetStudentPhotoQuery q, CancellationToken ct)
    {
        // Not allowed looks like not found, so other families' children can't be probed.
        if (!await access.CanViewStudentAsync(q.StudentId, ct)) return PhotoErrors.NotFound;
        var key = await db.Students.AsNoTracking().Where(x => x.Id == q.StudentId).Select(x => x.PhotoPath).FirstOrDefaultAsync(ct);
        return await photos.OpenAsync(key, ct);
    }
}
