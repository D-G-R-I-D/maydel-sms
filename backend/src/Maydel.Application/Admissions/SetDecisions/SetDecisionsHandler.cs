using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.SetDecisions;

internal sealed class SetDecisionsHandler(IAppDbContext db) : ICommandHandler<SetDecisionsCommand, IReadOnlyList<ExamCandidate>>
{
    public async Task<Result<IReadOnlyList<ExamCandidate>>> Handle(SetDecisionsCommand c, CancellationToken ct)
    {
        var ids = c.ApplicationIds.Distinct().ToList();
        var applications = await db.Applications.Where(a => ids.Contains(a.Id)).ToListAsync(ct);
        if (applications.Count != ids.Count) return AdmissionErrors.ApplicationNotFound;
        foreach (var application in applications)
            if (application.SetDecision(c.Decision) is { } invalid) return invalid.ToError();
        await db.SaveChangesAsync(ct);
        return await db.Candidates(db.Applications.AsNoTracking().Where(a => ids.Contains(a.Id))).ToListAsync(ct);
    }
}
