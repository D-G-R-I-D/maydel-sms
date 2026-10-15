using Maydel.Application.Abstractions;
using Maydel.Application.Common.Access;
using Maydel.Domain.Assessment;
using Maydel.Domain.Attendance;
using Maydel.Domain.Common;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Attendance;

/// <summary>Builds the register view: active students in the class, saved marks or the default "Present".</summary>
public sealed class RegisterReader(IAppDbContext db, AccessRules access, ISchoolClock clock, ICurrentUser me)
{
    public async Task<ClassRegister?> ReadAsync(Guid classRoomId, DateOnly date, CancellationToken ct)
    {
        var room = await db.ClassRooms.AsNoTracking().Where(r => r.Id == classRoomId)
            .Select(r => r.ClassLevel.Name + " " + r.Arm).FirstOrDefaultAsync(ct);
        if (room is null) return null;

        var term = await db.TermForAsync(date, ct);
        var problem = RegisterRules.CheckSchoolDay(date, term)?.Message;
        var canEdit = problem is null && RegisterRules.CheckEditable(date, clock.Today, me.IsInRole(Roles.Admin)) is null
            && await access.IsFormTeacherAsync(classRoomId, ct);

        var students = await db.Students.AsNoTracking().Where(s => s.ClassRoomId == classRoomId && s.Status == StudentStatus.Active)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Select(s => new { s.Id, s.AdmissionNumber, Name = s.LastName + ", " + s.FirstName }).ToListAsync(ct);
        var ids = students.Select(s => s.Id).ToList();
        var saved = await db.Attendance.AsNoTracking().Where(a => a.Date == date && ids.Contains(a.StudentId)).ToDictionaryAsync(a => a.StudentId, ct);
        var published = term is null ? [] : (await db.TermReports.AsNoTracking()
            .Where(r => r.TermId == term.Id && r.Status == ReportStatus.Published && ids.Contains(r.StudentId))
            .Select(r => r.StudentId).ToListAsync(ct)).ToHashSet();

        var entries = students.Select(s => saved.TryGetValue(s.Id, out var a)
            ? new RegisterEntry(s.Id, s.AdmissionNumber, s.Name, a.Status, a.Note, true, published.Contains(s.Id))
            : new RegisterEntry(s.Id, s.AdmissionNumber, s.Name, AttendanceStatus.Present, null, false, published.Contains(s.Id))).ToList();
        return new ClassRegister(classRoomId, room, date, term?.Id, term?.DisplayName, problem, saved.Count > 0, canEdit,
            entries.Count(e => e.Status == AttendanceStatus.Present), entries.Count(e => e.Status == AttendanceStatus.Absent),
            entries.Count(e => e.Status == AttendanceStatus.Late), entries.Count(e => e.Status == AttendanceStatus.Excused), entries,
            saved.Count == 0 ? null : saved.Values.Max(a => a.UpdatedAt ?? a.CreatedAt));
    }
}
