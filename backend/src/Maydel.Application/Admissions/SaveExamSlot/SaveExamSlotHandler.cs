using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Admissions;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.SaveExamSlot;

internal sealed class SaveExamSlotHandler(IAppDbContext db, ISchoolClock clock) : ICommandHandler<SaveExamSlotCommand, ExamSlotDto>
{
    public async Task<Result<ExamSlotDto>> Handle(SaveExamSlotCommand c, CancellationToken ct)
    {
        if (!await db.Sessions.AnyAsync(s => s.Id == c.SessionId, ct)) return AdmissionErrors.SessionNotFound;
        var startsAt = c.StartsAt.ToUniversalTime();
        ExamSlot? slot = null;
        var booked = 0;
        if (c.Id is { } id)
        {
            slot = await db.ExamSlots.FirstOrDefaultAsync(s => s.Id == id && s.SessionId == c.SessionId, ct);
            if (slot is null) return AdmissionErrors.SlotNotFound;
            booked = await db.Applications.CountAsync(a => a.ExamSlotId == id, ct);
            if (booked > 0 && (slot.StartsAt != startsAt || slot.DurationMinutes != c.DurationMinutes)) return AdmissionErrors.SlotHasBookings;
            if (c.Capacity < booked) return Error.Validation("capacity", $"{booked} applicants are already booked; seats can't be fewer than that.");
        }
        // Editing a sitting that already started (e.g. its notes) is allowed; moving it into the past isn't.
        if ((slot is null || slot.StartsAt != startsAt) && ExamSlot.Check(startsAt, c.DurationMinutes, c.Capacity, clock.Now) is { } invalid) return invalid.ToError();
        if (slot is not null && ExamSlot.Check(clock.Now.AddMinutes(1), c.DurationMinutes, c.Capacity, clock.Now) is { } shape) return shape.ToError();

        if (slot is null) db.ExamSlots.Add(slot = new ExamSlot { SessionId = c.SessionId, Venue = "" });
        (slot.StartsAt, slot.DurationMinutes, slot.Venue, slot.Mode, slot.Capacity, slot.Notes) =
            (startsAt, c.DurationMinutes, c.Venue.Trim(), c.Mode, c.Capacity, string.IsNullOrWhiteSpace(c.Notes) ? null : c.Notes.Trim());
        await db.SaveChangesAsync(ct);
        return new ExamSlotDto(slot.Id, slot.SessionId, slot.StartsAt, slot.DurationMinutes, slot.Venue, slot.Mode, slot.Capacity, booked, slot.Notes);
    }
}
