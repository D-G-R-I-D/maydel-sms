using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.GetAdmissionSettings;

internal sealed class GetAdmissionSettingsHandler(IAppDbContext db) : IQueryHandler<GetAdmissionSettingsQuery, AdmissionSettingsDto>
{
    public async Task<Result<AdmissionSettingsDto>> Handle(GetAdmissionSettingsQuery q, CancellationToken ct)
    {
        var session = await db.Sessions.AsNoTracking().Where(s => s.Id == q.SessionId).Select(s => s.Name).FirstOrDefaultAsync(ct);
        if (session is null) return AdmissionErrors.SessionNotFound;
        var s = await db.AdmissionSettings.AsNoTracking().FirstOrDefaultAsync(x => x.SessionId == q.SessionId, ct);
        return new AdmissionSettingsDto(q.SessionId, session, s?.IsOpen ?? false, s?.FormFee ?? 0, s?.ExamWindowDays ?? 5, s?.Instructions);
    }
}
