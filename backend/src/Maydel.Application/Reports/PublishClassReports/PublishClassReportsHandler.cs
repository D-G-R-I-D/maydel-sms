using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Reports.PublishClassReports;

internal sealed class PublishClassReportsHandler(IAppDbContext db, ReportBuilder reports, ICurrentUser me, TimeProvider clock)
    : ICommandHandler<PublishClassReportsCommand, PublishResult>
{
    public async Task<Result<PublishResult>> Handle(PublishClassReportsCommand c, CancellationToken ct)
    {
        if (!await db.ClassRooms.AnyAsync(r => r.Id == c.ClassRoomId, ct)) return ReportErrors.ClassNotFound;
        if (!await db.Terms.AnyAsync(t => t.Id == c.TermId, ct)) return ReportErrors.TermNotFound;

        var figures = await reports.ComputeAsync(c.ClassRoomId, c.TermId, ct);
        if (figures.Averages.Count == 0) return ReportErrors.NothingToPublish;

        var students = await db.Students.Where(s => s.ClassRoomId == c.ClassRoomId && s.Status == StudentStatus.Active)
            .Select(s => new { s.Id, Name = s.FirstName + " " + s.LastName }).ToListAsync(ct);
        var rows = await db.TermReports.Where(r => r.TermId == c.TermId && students.Select(s => s.Id).Contains(r.StudentId)).ToDictionaryAsync(r => r.StudentId, ct);

        var toPublish = students.Where(s => figures.Averages.ContainsKey(s.Id)).ToList();
        var missingAttendance = toPublish.Where(s => !rows.TryGetValue(s.Id, out var r) || r.DaysOpened == 0).Select(s => s.Name).ToList();
        if (missingAttendance.Count > 0) return ReportErrors.AttendanceMissing(missingAttendance);

        var now = clock.GetUtcNow();
        foreach (var s in toPublish)
        {
            var report = rows[s.Id];
            report.ClassRoomId = c.ClassRoomId;
            report.Publish(figures.Averages[s.Id], figures.ClassAverage, figures.Positions[s.Id], figures.Averages.Count, now, me.UserId);
        }
        await db.SaveChangesAsync(ct);

        var skipped = students.Where(s => !figures.Averages.ContainsKey(s.Id))
            .Select(s => new SkippedStudent(s.Id, s.Name, "No scores entered for this term.")).ToList();
        return new PublishResult(toPublish.Count, figures.ClassAverage, skipped);
    }
}
