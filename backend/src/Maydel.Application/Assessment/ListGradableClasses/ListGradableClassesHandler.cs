using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Assessment.ListGradableClasses;

internal sealed class ListGradableClassesHandler(IAppDbContext db, ICurrentUser me) : IQueryHandler<ListGradableClassesQuery, IReadOnlyList<GradableClass>>
{
    public async Task<Result<IReadOnlyList<GradableClass>>> Handle(ListGradableClassesQuery query, CancellationToken ct)
    {
        var offered = db.ClassSubjects.AsNoTracking();
        var uid = me.UserId;
        if (!me.IsInRole(Roles.Admin))
            offered = offered.Where(cs => (cs.Teacher != null && cs.Teacher.UserId == uid) || (cs.ClassRoom.FormTeacher != null && cs.ClassRoom.FormTeacher.UserId == uid));

        return await offered
            .OrderBy(cs => cs.ClassRoom.ClassLevel.Order).ThenBy(cs => cs.ClassRoom.Arm).ThenBy(cs => cs.Subject.Name)
            .Select(cs => new GradableClass(cs.ClassRoomId, cs.ClassRoom.ClassLevel.Name + " " + cs.ClassRoom.Arm, cs.ClassRoom.ClassLevel.Order,
                cs.SubjectId, cs.Subject.Name, cs.ClassRoom.FormTeacher != null && cs.ClassRoom.FormTeacher.UserId == uid))
            .ToListAsync(ct);
    }
}
