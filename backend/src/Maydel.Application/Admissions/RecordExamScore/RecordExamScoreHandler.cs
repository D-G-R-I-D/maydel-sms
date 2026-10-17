using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.RecordExamScore;

internal sealed class RecordExamScoreHandler(IAppDbContext db, ISchoolClock clock) : ICommandHandler<RecordExamScoreCommand, ExamCandidate>
{
    public async Task<Result<ExamCandidate>> Handle(RecordExamScoreCommand c, CancellationToken ct)
    {
        var application = await db.Applications.FirstOrDefaultAsync(a => a.Id == c.ApplicationId, ct);
        if (application is null) return AdmissionErrors.ApplicationNotFound;
        if (application.RecordScore(c.Score, clock.Now.ToUniversalTime()) is { } invalid) return invalid.ToError();
        await db.SaveChangesAsync(ct);
        return await db.Candidates(db.Applications.AsNoTracking().Where(a => a.Id == application.Id)).FirstAsync(ct);
    }
}
