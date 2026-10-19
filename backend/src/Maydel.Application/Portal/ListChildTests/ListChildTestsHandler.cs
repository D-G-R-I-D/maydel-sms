using Maydel.Application.Abstractions;
using Maydel.Application.Cbt;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Assessment;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Portal.ListChildTests;

internal sealed class ListChildTestsHandler(IAppDbContext db, AccessRules access) : IQueryHandler<ListChildTestsQuery, IReadOnlyList<ChildTestResult>>
{
    public async Task<Result<IReadOnlyList<ChildTestResult>>> Handle(ListChildTestsQuery q, CancellationToken ct)
    {
        if (!await access.PortalStudents().AnyAsync(s => s.Id == q.StudentId, ct)) return PortalErrors.StudentNotFound;
        var attempts = await db.TestAttempts.AsNoTracking().Where(a => a.StudentId == q.StudentId).ToListAsync(ct);
        var testIds = attempts.Select(a => a.TestId).Distinct().ToList();
        var tests = await db.CbtTests.AsNoTracking().Where(t => testIds.Contains(t.Id) && t.ResultsReleasedAt != null).ToListAsync(ct);
        var subjects = await db.Subjects.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        return tests.Select(t => (t, best: Attempts.Best(attempts.Where(a => a.TestId == t.Id))))
            .Where(x => x.best is not null)
            .Select(x => new ChildTestResult(x.t.Id, x.t.Title, subjects[x.t.SubjectId], x.best!.SubmittedAt, x.best.TotalScore!.Value, x.t.TotalMarks,
                x.t.TotalMarks <= 0 ? 0 : ReportMath.Round(x.best.TotalScore.Value / x.t.TotalMarks * 100)))
            .OrderByDescending(r => r.SubmittedAt)
            .ToList();
    }
}
