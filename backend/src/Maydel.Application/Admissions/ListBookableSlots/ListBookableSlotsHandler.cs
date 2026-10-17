using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Attendance;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.ListBookableSlots;

internal sealed class ListBookableSlotsHandler(IAppDbContext db, ICurrentUser me, ISchoolClock clock) : IQueryHandler<ListBookableSlotsQuery, IReadOnlyList<ExamSlotOption>>
{
    public async Task<Result<IReadOnlyList<ExamSlotOption>>> Handle(ListBookableSlotsQuery q, CancellationToken ct)
    {
        var app = await db.Applications.AsNoTracking().Where(a => a.Id == q.ApplicationId && a.ApplicantUserId == me.UserId)
            .Select(a => new { a.SessionId, a.ExamWindowEndsOn, a.ExamSlotId }).FirstOrDefaultAsync(ct);
        if (app is null) return AdmissionErrors.ApplicationNotFound;
        if (app.ExamWindowEndsOn is not { } last) return Array.Empty<ExamSlotOption>();

        var now = clock.Now.ToUniversalTime();
        // Midnight after the last day, in Abuja.
        var until = new DateTimeOffset(last.AddDays(1).ToDateTime(TimeOnly.MinValue), RegisterRules.SchoolUtcOffset).ToUniversalTime();
        var slots = await db.ExamSlots.AsNoTracking()
            .Where(s => s.SessionId == app.SessionId && s.StartsAt > now && s.StartsAt < until)
            .OrderBy(s => s.StartsAt)
            .Select(s => new { s.Id, s.StartsAt, s.DurationMinutes, s.Venue, s.Mode, s.Capacity, Taken = db.Applications.Count(a => a.ExamSlotId == s.Id) })
            .ToListAsync(ct);
        return slots.Where(s => s.Taken < s.Capacity || s.Id == app.ExamSlotId)
            .Select(s => new ExamSlotOption(s.Id, s.StartsAt, s.StartsAt.AddMinutes(s.DurationMinutes), s.Venue, s.Mode, Math.Max(0, s.Capacity - s.Taken), s.Id == app.ExamSlotId))
            .ToList();
    }
}
