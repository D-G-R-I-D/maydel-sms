using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Admissions;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.GetExamResults;

internal sealed class GetExamResultsHandler(IAppDbContext db) : IQueryHandler<GetExamResultsQuery, ExamResults>
{
    public async Task<Result<ExamResults>> Handle(GetExamResultsQuery q, CancellationToken ct)
    {
        if (!await db.Sessions.AnyAsync(s => s.Id == q.SessionId, ct)) return AdmissionErrors.SessionNotFound;
        var cutOff = await db.AdmissionSettings.Where(s => s.SessionId == q.SessionId).Select(s => s.CutOffMark).FirstOrDefaultAsync(ct);
        var apps = db.Applications.AsNoTracking().Where(a => a.SessionId == q.SessionId);
        if (q.ClassLevelId is { } level) apps = apps.Where(a => a.ClassLevelId == level);

        // Everyone with a score, including those already offered or not (later stages keep the score).
        var taken = (await db.Candidates(apps.Where(a => a.ExamScore != null)).ToListAsync(ct))
            .OrderByDescending(c => c.Score).ThenBy(c => c.ChildName).ToList();
        var awaiting = await apps.CountAsync(a => a.Status == ApplicationStatus.ExamBooked, ct);
        var missed = await apps.CountAsync(a => a.Status == ApplicationStatus.Missed, ct);
        return cutOff is { } mark
            ? new ExamResults(q.SessionId, mark, taken, [.. taken.Where(c => c.Score >= mark)], [.. taken.Where(c => c.Score < mark)], awaiting, missed)
            : new ExamResults(q.SessionId, null, taken, [], [], awaiting, missed);
    }
}
