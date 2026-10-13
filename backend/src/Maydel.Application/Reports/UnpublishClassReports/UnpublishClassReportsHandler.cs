using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Assessment;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Reports.UnpublishClassReports;

internal sealed class UnpublishClassReportsHandler(IAppDbContext db) : ICommandHandler<UnpublishClassReportsCommand, UnpublishResult>
{
    public async Task<Result<UnpublishResult>> Handle(UnpublishClassReportsCommand c, CancellationToken ct)
    {
        if (!await db.ClassRooms.AnyAsync(r => r.Id == c.ClassRoomId, ct)) return ReportErrors.ClassNotFound;
        var reports = db.TermReports.Where(r => r.ClassRoomId == c.ClassRoomId && r.TermId == c.TermId && r.Status == ReportStatus.Published);
        if (c.StudentIds is { Count: > 0 } ids) reports = reports.Where(r => ids.Contains(r.StudentId));

        var list = await reports.ToListAsync(ct); // tracked, so each change is audited
        foreach (var report in list) report.Unpublish();
        await db.SaveChangesAsync(ct);
        return new UnpublishResult(list.Count);
    }
}
