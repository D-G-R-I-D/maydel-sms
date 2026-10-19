using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Admissions;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.NotifyStageOnePasses;

internal sealed class NotifyStageOnePassesHandler(IAppDbContext db, ISchoolClock clock, AdmissionEmails emails) : ICommandHandler<NotifyStageOnePassesCommand, NotifyResult>
{
    public async Task<Result<NotifyResult>> Handle(NotifyStageOnePassesCommand c, CancellationToken ct)
    {
        if (!await db.Sessions.AnyAsync(s => s.Id == c.SessionId, ct)) return AdmissionErrors.SessionNotFound;
        var cutOff = await db.AdmissionSettings.Where(s => s.SessionId == c.SessionId).Select(s => s.CutOffMark).FirstOrDefaultAsync(ct);
        if (cutOff is not { } mark) return AdmissionErrors.NoCutOff;

        var passed = await db.Applications
            .Where(a => a.SessionId == c.SessionId && a.Status == ApplicationStatus.Assessed && a.ExamScore >= mark && a.StageOneNotifiedAt == null)
            .ToListAsync(ct);
        var now = clock.Now.ToUniversalTime();
        foreach (var application in passed)
        {
            application.StageOneNotifiedAt = now;
            await emails.QueueAsync(application, "Entrance exam: first stage passed",
                $"Congratulations! {application.ChildName} passed the first stage of our admission process. " +
                "The school will contact you shortly about the next step.", ct);
        }
        await db.SaveChangesAsync(ct);
        var already = await db.Applications.CountAsync(a => a.SessionId == c.SessionId && a.ExamScore >= mark && a.StageOneNotifiedAt != null && a.StageOneNotifiedAt != now, ct);
        return new NotifyResult(passed.Count, already);
    }
}
