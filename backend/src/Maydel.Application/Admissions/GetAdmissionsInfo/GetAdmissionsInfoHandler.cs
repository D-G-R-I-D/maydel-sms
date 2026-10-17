using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.GetAdmissionsInfo;

internal sealed class GetAdmissionsInfoHandler(IAppDbContext db) : IQueryHandler<GetAdmissionsInfoQuery, IReadOnlyList<AdmissionsInfo>>
{
    public async Task<Result<IReadOnlyList<AdmissionsInfo>>> Handle(GetAdmissionsInfoQuery q, CancellationToken ct)
    {
        var levels = await db.ClassLevels.AsNoTracking().OrderBy(l => l.Order).Select(l => new ClassLevelOption(l.Id, l.Name)).ToListAsync(ct);
        var open = await db.AdmissionSettings.AsNoTracking().Where(s => s.IsOpen)
            .Join(db.Sessions, a => a.SessionId, s => s.Id, (a, s) => new { a, s })
            .OrderBy(x => x.s.StartsOn)
            .Select(x => new { x.s.Id, x.s.Name, x.a.IsOpen, x.a.FormFee, x.a.ExamWindowDays, x.a.Instructions }).ToListAsync(ct);
        return open.Select(o => new AdmissionsInfo(o.Id, o.Name, o.IsOpen, o.FormFee, o.ExamWindowDays, o.Instructions, levels)).ToList();
    }
}
