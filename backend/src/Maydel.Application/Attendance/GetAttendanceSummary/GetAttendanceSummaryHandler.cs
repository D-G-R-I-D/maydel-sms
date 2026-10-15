using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Assessment;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Attendance.GetAttendanceSummary;

internal sealed class GetAttendanceSummaryHandler(IAppDbContext db, AccessRules access) : IQueryHandler<GetAttendanceSummaryQuery, ClassAttendanceSummary>
{
    public async Task<Result<ClassAttendanceSummary>> Handle(GetAttendanceSummaryQuery q, CancellationToken ct)
    {
        if (!await access.TeachesClassAsync(q.ClassRoomId, ct)) return AttendanceErrors.NotAllowed;
        var room = await db.ClassRooms.AsNoTracking().Where(r => r.Id == q.ClassRoomId).Select(r => r.ClassLevel.Name + " " + r.Arm).FirstOrDefaultAsync(ct);
        if (room is null) return AttendanceErrors.ClassNotFound;
        var term = await db.Terms.AsNoTracking().Include(t => t.Session).FirstOrDefaultAsync(t => t.Id == q.TermId, ct);
        if (term is null) return AttendanceErrors.TermNotFound;

        var totals = await db.ForClassAsync(q.ClassRoomId, term, ct);
        var students = await db.Students.AsNoTracking().Where(s => s.ClassRoomId == q.ClassRoomId && s.Status == StudentStatus.Active)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Select(s => new { s.Id, s.AdmissionNumber, Name = s.LastName + ", " + s.FirstName }).ToListAsync(ct);
        var rows = students.Select(s =>
        {
            var t = totals.ByStudent.GetValueOrDefault(s.Id);
            decimal? pct = totals.DaysOpened == 0 ? null : ReportMath.Round(100m * t.Present / totals.DaysOpened);
            return new StudentAttendance(s.Id, s.AdmissionNumber, s.Name, totals.DaysOpened, t.Present, t.Absent, t.Late, t.Excused, pct);
        }).ToList();
        return new ClassAttendanceSummary(q.ClassRoomId, room, term.Id, term.DisplayName, totals.DaysOpened, rows);
    }
}
