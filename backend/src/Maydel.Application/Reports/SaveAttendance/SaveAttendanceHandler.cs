using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Common.Access;
using Maydel.Application.Reports.GetClassReports;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Reports.SaveAttendance;

internal sealed class SaveAttendanceHandler(
    IAppDbContext db, AccessRules access, ReportBuilder reports, IQueryHandler<GetClassReportsQuery, ClassReportSummary> summary)
    : ICommandHandler<SaveAttendanceCommand, ClassReportSummary>
{
    public async Task<Result<ClassReportSummary>> Handle(SaveAttendanceCommand c, CancellationToken ct)
    {
        if (!await db.ClassRooms.AnyAsync(r => r.Id == c.ClassRoomId, ct)) return ReportErrors.ClassNotFound;
        if (!await access.IsFormTeacherAsync(c.ClassRoomId, ct)) return ReportErrors.NotFormTeacher;
        if (!await db.Terms.AnyAsync(t => t.Id == c.TermId, ct)) return ReportErrors.TermNotFound;

        var active = (await db.Students.Where(s => s.ClassRoomId == c.ClassRoomId && s.Status == StudentStatus.Active).Select(s => s.Id).ToListAsync(ct)).ToHashSet();
        var errors = new Dictionary<string, string[]>();
        for (var i = 0; i < c.Entries.Count; i++)
        {
            if (!active.Contains(c.Entries[i].StudentId)) errors[$"entries[{i}].studentId"] = ["This student isn't an active member of the class."];
            else if (c.Entries[i].DaysPresent < 0 || c.Entries[i].DaysPresent > c.DaysOpened)
                errors[$"entries[{i}].daysPresent"] = [$"Days present must be between 0 and {c.DaysOpened}."];
        }
        if (errors.Count > 0) return new ValidationError(errors);

        foreach (var entry in c.Entries)
        {
            var report = await reports.GetOrCreateAsync(entry.StudentId, c.TermId, c.ClassRoomId, ct);
            if (report.SetAttendance(c.DaysOpened, entry.DaysPresent) is { } invalid) return invalid.ToError();
        }
        await db.SaveChangesAsync(ct);
        return await summary.Handle(new GetClassReportsQuery(c.ClassRoomId, c.TermId), ct);
    }
}
