using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Portal.ListMyChildren;

internal sealed class ListMyChildrenHandler(IAppDbContext db, AccessRules access) : IQueryHandler<ListMyChildrenQuery, IReadOnlyList<PortalChild>>
{
    public async Task<Result<IReadOnlyList<PortalChild>>> Handle(ListMyChildrenQuery query, CancellationToken ct)
    {
        var children = await access.PortalStudents().AsNoTracking().OrderBy(s => s.FirstName).ThenBy(s => s.LastName)
            .Select(s => new
            {
                s.Id, s.FirstName, s.MiddleName, s.LastName, s.AdmissionNumber, s.PhotoPath,
                ClassRoom = s.ClassRoom != null ? s.ClassRoom.ClassLevel.Name + " " + s.ClassRoom.Arm : null,
            })
            .ToListAsync(ct);
        var ids = children.Select(c => c.Id).ToList();
        var results = await db.PublishedResults(ids).ToListAsync(ct);

        return children.Select(c =>
        {
            var summary = results.FirstOrDefault(r => r.StudentId == c.Id)?.Result; // newest first
            var name = string.Join(' ', new[] { c.FirstName, c.MiddleName, c.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
            return new PortalChild(c.Id, name, c.AdmissionNumber, c.ClassRoom, Photos.PhotoStore.VersionOf(c.PhotoPath), summary);
        }).ToList();
    }
}
