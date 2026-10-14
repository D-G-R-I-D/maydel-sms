using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Reports;
using Maydel.Domain.Assessment;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Portal;

/// <summary>Loads a report card for the portal: only the caller's own children (or themselves), and only once published.</summary>
public sealed class PortalReports(IAppDbContext db, AccessRules access, ReportBuilder reports)
{
    public async Task<Result<ReportCard>> PublishedCardAsync(Guid studentId, Guid termId, CancellationToken ct)
    {
        if (!await access.PortalStudents().AnyAsync(s => s.Id == studentId, ct)) return PortalErrors.StudentNotFound;
        var classRoomId = await db.TermReports.AsNoTracking()
            .Where(r => r.StudentId == studentId && r.TermId == termId && r.Status == ReportStatus.Published)
            .Select(r => (Guid?)r.ClassRoomId).FirstOrDefaultAsync(ct);
        if (classRoomId is null) return PortalErrors.NotPublished;
        return await reports.BuildAsync(studentId, termId, classRoomId.Value, ct) is { } card ? card : PortalErrors.NotPublished;
    }
}
