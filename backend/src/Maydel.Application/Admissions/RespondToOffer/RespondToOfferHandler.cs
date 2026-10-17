using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.RespondToOffer;

internal sealed class RespondToOfferHandler(IAppDbContext db, ICurrentUser me, ISchoolClock clock, AdmissionEmails emails) : ICommandHandler<RespondToOfferCommand, ApplicationDetail>
{
    public async Task<Result<ApplicationDetail>> Handle(RespondToOfferCommand c, CancellationToken ct)
    {
        if (await db.MineAsync(c.ApplicationId, me.UserId, ct) is not { } application) return AdmissionErrors.ApplicationNotFound;
        if (application.Respond(c.Accept, clock.Now.ToUniversalTime()) is { } invalid) return invalid.ToError();
        await emails.QueueAsync(application, c.Accept ? "Offer accepted" : "Offer declined", c.Accept
            ? $"Thank you for accepting the offer for {application.ChildName}. The school will complete enrolment and email you when the parent portal is ready."
            : $"You have declined the offer of admission for {application.ChildName}. If this was a mistake, please contact the school.", ct);
        await db.SaveChangesAsync(ct);
        return (await db.DetailAsync(application.Id, ct))!;
    }
}
