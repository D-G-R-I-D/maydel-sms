using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.ReopenApplication;

internal sealed class ReopenApplicationHandler(IAppDbContext db, ISchoolClock clock, AdmissionEmails emails) : ICommandHandler<ReopenApplicationCommand, ApplicationDetail>
{
    public async Task<Result<ApplicationDetail>> Handle(ReopenApplicationCommand c, CancellationToken ct)
    {
        var application = await db.Applications.FirstOrDefaultAsync(a => a.Id == c.ApplicationId, ct);
        if (application is null) return AdmissionErrors.ApplicationNotFound;
        var days = await db.AdmissionSettings.Where(s => s.SessionId == application.SessionId).Select(s => (int?)s.ExamWindowDays).FirstOrDefaultAsync(ct) ?? 5;
        if (application.Reopen(clock.Today, days) is { } invalid) return invalid.ToError();
        await emails.QueueAsync(application, "Entrance exam window reopened",
            $"The school has reopened {application.ChildName}'s application. Please book and take the entrance exam by {application.ExamWindowEndsOn:dddd d MMMM yyyy}.", ct);
        await db.SaveChangesAsync(ct);
        return (await db.DetailAsync(application.Id, ct, asStaff: true))!;
    }
}
