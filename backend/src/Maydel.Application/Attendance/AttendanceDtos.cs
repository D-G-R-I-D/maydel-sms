using Maydel.Application.Abstractions;
using Maydel.Domain.Academics;
using Maydel.Domain.Attendance;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Attendance;

/// <summary><c>Recorded</c>: saved for this day (otherwise the default "Present" is shown). <c>Locked</c>: the student's report for the term is published.</summary>
public sealed record RegisterEntry(
    Guid StudentId, string AdmissionNumber, string Name, AttendanceStatus Status, string? Note, bool Recorded, bool Locked);

/// <summary>
/// One class's register for one day (E9.1). <c>Problem</c> explains why there can't be a register that day (weekend,
/// mid-term, outside a term). <c>Taken</c>: saved at least once. <c>CanEdit</c>: the caller may save it now.
/// </summary>
public sealed record ClassRegister(
    Guid ClassRoomId, string ClassRoom, DateOnly Date, Guid? TermId, string? Term, string? Problem, bool Taken, bool CanEdit,
    int Present, int Absent, int Late, int Excused, IReadOnlyList<RegisterEntry> Entries);

public sealed record RegisterEntryInput(Guid StudentId, AttendanceStatus Status, string? Note);

/// <summary>Term totals for one student (E9.2). <c>Percentage</c>: present (incl. late) ÷ days opened, null before any register.</summary>
public sealed record StudentAttendance(
    Guid StudentId, string AdmissionNumber, string Name, int DaysOpened, int Present, int Absent, int Late, int Excused, decimal? Percentage);

/// <summary><c>DaysOpened</c>: days this class's register was taken in the term.</summary>
public sealed record ClassAttendanceSummary(
    Guid ClassRoomId, string ClassRoom, Guid TermId, string Term, int DaysOpened, IReadOnlyList<StudentAttendance> Rows);

public static class AttendanceErrors
{
    public static readonly Common.Error NotAllowed = Common.Error.Forbidden("attendance.not_allowed", "Only administrators and this class's teachers can see its register.");
    public static readonly Common.Error NotFormTeacher = Common.Error.Forbidden("attendance.not_form_teacher", "Only the class's form teacher or an administrator can take the register.");
    public static readonly Common.Error ClassNotFound = Common.Error.NotFound("classroom.not_found", "That class doesn't exist.");
    public static readonly Common.Error TermNotFound = Common.Error.NotFound("term.not_found", "That term doesn't exist.");
    public static Common.Error ReportPublished(IEnumerable<string> names) => Common.Error.Rule("attendance.report_published",
        $"These students' reports for the term are published, so their attendance can't change: {string.Join(", ", names)}. An administrator must unpublish first.");
}

/// <summary>Register totals for a class and term: shared by the summary, the report card and publishing.</summary>
public static class AttendanceTotals
{
    public sealed record Totals(int DaysOpened, IReadOnlyDictionary<Guid, (int Present, int Absent, int Late, int Excused)> ByStudent);

    public static async Task<Term?> TermForAsync(this IAppDbContext db, DateOnly date, CancellationToken ct) =>
        await db.Terms.AsNoTracking().Include(t => t.Session).Where(t => t.StartsOn <= date && t.EndsOn >= date)
            .OrderBy(t => t.StartsOn).FirstOrDefaultAsync(ct);

    public static async Task<Totals> ForClassAsync(this IAppDbContext db, Guid classRoomId, Term term, CancellationToken ct)
    {
        var records = await db.Attendance.AsNoTracking()
            .Where(a => a.ClassRoomId == classRoomId && a.Date >= term.StartsOn && a.Date <= term.EndsOn)
            .Select(a => new { a.StudentId, a.Date, a.Status }).ToListAsync(ct);
        var byStudent = records.GroupBy(r => r.StudentId).ToDictionary(g => g.Key, g => (
            g.Count(r => r.Status is AttendanceStatus.Present or AttendanceStatus.Late),
            g.Count(r => r.Status == AttendanceStatus.Absent),
            g.Count(r => r.Status == AttendanceStatus.Late),
            g.Count(r => r.Status == AttendanceStatus.Excused)));
        return new Totals(records.Select(r => r.Date).Distinct().Count(), byStudent);
    }
}
