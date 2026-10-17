using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Communication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Maydel.Application.Admissions.SubmitApplication;

internal sealed class SubmitApplicationHandler(IAppDbContext db, ICurrentUser me, TimeProvider time, IOptions<EmailOptions> options)
    : ICommandHandler<SubmitApplicationCommand, ApplicationDetail>
{
    public async Task<Result<ApplicationDetail>> Handle(SubmitApplicationCommand c, CancellationToken ct)
    {
        if (await db.MineAsync(c.ApplicationId, me.UserId, ct) is not { } application) return AdmissionErrors.ApplicationNotFound;
        var now = time.GetUtcNow();
        if (application.Submit(now) is { } invalid) return invalid.ToError();

        var school = await db.SchoolProfiles.AsNoTracking().OrderBy(p => p.CreatedAt).Select(p => p.Name).FirstOrDefaultAsync(ct) ?? options.Value.FromName;
        db.OutboxEmails.Add(new OutboxEmail
        {
            Kind = EmailKind.AdmissionStage, ToAddress = application.Email, ToName = application.ParentName,
            Subject = $"Application received – {application.ChildName} ({application.ReferenceNumber})",
            Body = $"Dear {application.ParentName},\n\nWe've received the admission form for {application.ChildName} (reference {application.ReferenceNumber}). " +
                   $"The next step is the entrance exam: please book and take it by {application.ExamWindowEndsOn:dddd, d MMMM yyyy}.\n\n{school}",
            NextAttemptAt = now,
        });
        await db.SaveChangesAsync(ct);
        return (await db.DetailAsync(application.Id, ct))!;
    }
}
