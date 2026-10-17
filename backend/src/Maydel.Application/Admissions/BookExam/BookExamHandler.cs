using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Admissions;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.BookExam;

internal sealed class BookExamHandler(IAppDbContext db, ICurrentUser me, ISchoolClock clock, AdmissionEmails emails) : ICommandHandler<BookExamCommand, ApplicationDetail>
{
    public async Task<Result<ApplicationDetail>> Handle(BookExamCommand c, CancellationToken ct)
    {
        if (await db.MineAsync(c.ApplicationId, me.UserId, ct) is not { } application) return AdmissionErrors.ApplicationNotFound;
        var slot = await db.ExamSlots.FirstOrDefaultAsync(s => s.Id == c.SlotId && s.SessionId == application.SessionId, ct);
        if (slot is null) return Error.Validation("slotId", "That exam time isn't available.");
        if (application.ExamSlotId == slot.Id) return (await db.DetailAsync(application.Id, ct))!;

        var current = application.ExamSlotId is { } currentId
            ? await db.ExamSlots.Where(s => s.Id == currentId).Select(s => (DateTimeOffset?)s.StartsAt).FirstOrDefaultAsync(ct)
            : null;
        var taken = await db.Applications.CountAsync(a => a.ExamSlotId == slot.Id, ct);
        var now = clock.Now.ToUniversalTime();
        if (application.BookExam(slot, taken, current, now) is { } invalid) return invalid.ToError();

        slot.Version++;
        await emails.QueueAsync(application, "Entrance exam booked",
            $"{application.ChildName}'s entrance exam is booked for {AdmissionEmails.When(slot.StartsAt)} at {slot.Venue}" +
            (slot.Mode == ExamMode.Cbt ? " (on a computer at the school)" : "") +
            ". Please arrive 15 minutes early with a pencil, pen and eraser. We'll send a reminder the day before.", ct);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AdmissionErrors.SlotTaken;
        }
        return (await db.DetailAsync(application.Id, ct))!;
    }
}
