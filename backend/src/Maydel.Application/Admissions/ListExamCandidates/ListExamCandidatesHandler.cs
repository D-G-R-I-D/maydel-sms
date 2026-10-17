using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.ListExamCandidates;

internal sealed class ListExamCandidatesHandler(IAppDbContext db) : IQueryHandler<ListExamCandidatesQuery, IReadOnlyList<ExamCandidate>>
{
    public async Task<Result<IReadOnlyList<ExamCandidate>>> Handle(ListExamCandidatesQuery q, CancellationToken ct)
    {
        if (!await db.ExamSlots.AnyAsync(s => s.Id == q.SlotId, ct)) return AdmissionErrors.SlotNotFound;
        return await db.Candidates(db.Applications.AsNoTracking().Where(a => a.ExamSlotId == q.SlotId)).ToListAsync(ct);
    }
}
