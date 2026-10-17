using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Admissions.GetAdmissionSettings;
using Maydel.Domain.Admissions;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.SaveAdmissionSettings;

internal sealed class SaveAdmissionSettingsHandler(IAppDbContext db, IQueryHandler<GetAdmissionSettingsQuery, AdmissionSettingsDto> get)
    : ICommandHandler<SaveAdmissionSettingsCommand, AdmissionSettingsDto>
{
    public async Task<Result<AdmissionSettingsDto>> Handle(SaveAdmissionSettingsCommand c, CancellationToken ct)
    {
        if (!await db.Sessions.AnyAsync(s => s.Id == c.SessionId, ct)) return AdmissionErrors.SessionNotFound;
        if (AdmissionSettings.Check(c.FormFee, c.ExamWindowDays) is { } invalid) return invalid.ToError();
        var s = await db.AdmissionSettings.FirstOrDefaultAsync(x => x.SessionId == c.SessionId, ct);
        if (s is null) db.AdmissionSettings.Add(s = new AdmissionSettings { SessionId = c.SessionId });
        (s.IsOpen, s.FormFee, s.ExamWindowDays, s.Instructions) = (c.IsOpen, c.FormFee, c.ExamWindowDays, string.IsNullOrWhiteSpace(c.Instructions) ? null : c.Instructions.Trim());
        await db.SaveChangesAsync(ct);
        return await get.Handle(new GetAdmissionSettingsQuery(c.SessionId), ct);
    }
}
