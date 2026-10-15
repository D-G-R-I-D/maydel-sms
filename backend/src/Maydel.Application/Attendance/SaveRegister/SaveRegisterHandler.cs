using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Assessment;
using Maydel.Domain.Attendance;
using Maydel.Domain.Common;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Attendance.SaveRegister;

internal sealed class SaveRegisterHandler(IAppDbContext db, AccessRules access, RegisterReader reader, ISchoolClock clock, ICurrentUser me)
    : ICommandHandler<SaveRegisterCommand, ClassRegister>
{
    public async Task<Result<ClassRegister>> Handle(SaveRegisterCommand c, CancellationToken ct)
    {
        if (!await db.ClassRooms.AnyAsync(r => r.Id == c.ClassRoomId, ct)) return AttendanceErrors.ClassNotFound;
        if (!await access.IsFormTeacherAsync(c.ClassRoomId, ct)) return AttendanceErrors.NotFormTeacher;
        var term = await db.TermForAsync(c.Date, ct);
        if (RegisterRules.CheckSchoolDay(c.Date, term) is { } notSchoolDay) return notSchoolDay.ToError();
        if (RegisterRules.CheckEditable(c.Date, clock.Today, me.IsInRole(Roles.Admin)) is { } notEditable) return notEditable.ToError();

        var students = await db.Students.Where(s => s.ClassRoomId == c.ClassRoomId && s.Status == StudentStatus.Active)
            .Select(s => new { s.Id, Name = s.FirstName + " " + s.LastName }).ToListAsync(ct);
        var active = students.Select(s => s.Id).ToHashSet();
        var errors = new Dictionary<string, string[]>();
        for (var i = 0; i < c.Entries.Count; i++)
            if (!active.Contains(c.Entries[i].StudentId)) errors[$"entries[{i}].studentId"] = ["This student isn't an active member of the class."];
        if (errors.Count > 0) return new ValidationError(errors);

        var marks = students.ToDictionary(s => s.Id, s => c.Entries.FirstOrDefault(e => e.StudentId == s.Id) ?? new RegisterEntryInput(s.Id, AttendanceStatus.Present, null));
        var existing = await db.Attendance.Where(a => a.Date == c.Date && active.Contains(a.StudentId)).ToDictionaryAsync(a => a.StudentId, ct);
        var published = (await db.TermReports.Where(r => r.TermId == term!.Id && r.Status == ReportStatus.Published && active.Contains(r.StudentId))
            .Select(r => r.StudentId).ToListAsync(ct)).ToHashSet();

        // A published report froze the term's attendance: refuse any change that would contradict it.
        var locked = students.Where(s => published.Contains(s.Id) && Changes(existing.GetValueOrDefault(s.Id), marks[s.Id])).Select(s => s.Name).ToList();
        if (locked.Count > 0) return AttendanceErrors.ReportPublished(locked);

        foreach (var (studentId, mark) in marks)
        {
            if (published.Contains(studentId)) continue;
            if (!existing.TryGetValue(studentId, out var record))
                db.Attendance.Add(record = new AttendanceRecord { StudentId = studentId, ClassRoomId = c.ClassRoomId, Date = c.Date });
            record.ClassRoomId = c.ClassRoomId;
            record.Set(mark.Status, mark.Note, me.UserId);
        }
        await db.SaveChangesAsync(ct);
        return (await reader.ReadAsync(c.ClassRoomId, c.Date, ct))!;
    }

    private static bool Changes(AttendanceRecord? current, RegisterEntryInput mark) =>
        current is null || current.Status != mark.Status || (current.Note ?? "") != (mark.Note?.Trim() ?? "");
}
