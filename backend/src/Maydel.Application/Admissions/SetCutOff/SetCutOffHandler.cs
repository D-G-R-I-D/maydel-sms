using Maydel.Application.Abstractions;
using Maydel.Application.Admissions.GetExamResults;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Admissions;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.SetCutOff;

internal sealed class SetCutOffHandler(IAppDbContext db, IQueryHandler<GetExamResultsQuery, ExamResults> results) : ICommandHandler<SetCutOffCommand, ExamResults>
{
    public async Task<Result<ExamResults>> Handle(SetCutOffCommand c, CancellationToken ct)
    {
        if (!await db.Sessions.AnyAsync(s => s.Id == c.SessionId, ct)) return AdmissionErrors.SessionNotFound;
        if (c.CutOffMark is { } mark && (mark is < 0 or > 100 || decimal.Round(mark, 2) != mark))
            return Error.Validation("cutOffMark", "Enter a cut-off from 0 to 100 (at most 2 decimal places).");
        var s = await db.AdmissionSettings.FirstOrDefaultAsync(x => x.SessionId == c.SessionId, ct);
        if (s is null) db.AdmissionSettings.Add(s = new AdmissionSettings { SessionId = c.SessionId });
        s.CutOffMark = c.CutOffMark;
        await db.SaveChangesAsync(ct);
        return await results.Handle(new GetExamResultsQuery(c.SessionId), ct);
    }
}
