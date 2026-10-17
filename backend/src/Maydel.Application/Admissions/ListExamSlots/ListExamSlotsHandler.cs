using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.ListExamSlots;

internal sealed class ListExamSlotsHandler(IAppDbContext db) : IQueryHandler<ListExamSlotsQuery, IReadOnlyList<ExamSlotDto>>
{
    public async Task<Result<IReadOnlyList<ExamSlotDto>>> Handle(ListExamSlotsQuery q, CancellationToken ct) =>
        await db.ExamSlots.AsNoTracking().Where(s => s.SessionId == q.SessionId).OrderBy(s => s.StartsAt)
            .Select(s => new ExamSlotDto(s.Id, s.SessionId, s.StartsAt, s.DurationMinutes, s.Venue, s.Mode, s.Capacity,
                db.Applications.Count(a => a.ExamSlotId == s.Id), s.Notes))
            .ToListAsync(ct);
}
