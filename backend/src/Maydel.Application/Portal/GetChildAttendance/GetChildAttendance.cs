using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Assessment;
using Maydel.Domain.Attendance;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Portal.GetChildAttendance;

public sealed record AttendanceDay(DateOnly Date, AttendanceStatus Status, string? Note);

/// <summary>
/// A child's attendance for a term (E9.2 in the portal): the days the register was taken, with totals. <c>Percent</c> counts
/// late as present, like the report card.
/// </summary>
public sealed record ChildAttendance(Guid StudentId, Guid TermId, string Term, int DaysRecorded, int Present, int Absent, int Late, int Excused,
    decimal? Percent, IReadOnlyList<AttendanceDay> Days);

/// <summary>The child's register marks for a term (default: the current term).</summary>
public sealed record GetChildAttendanceQuery(Guid StudentId, Guid? TermId = null) : IQuery<ChildAttendance>;

internal sealed class GetChildAttendanceHandler(IAppDbContext db, AccessRules access) : IQueryHandler<GetChildAttendanceQuery, ChildAttendance>
{
    public async Task<Result<ChildAttendance>> Handle(GetChildAttendanceQuery q, CancellationToken ct)
    {
        if (!await access.PortalStudents().AnyAsync(s => s.Id == q.StudentId, ct)) return PortalErrors.StudentNotFound;
        var terms = db.Terms.AsNoTracking().Include(t => t.Session);
        var term = q.TermId is { } id ? await terms.FirstOrDefaultAsync(t => t.Id == id, ct) : await terms.FirstOrDefaultAsync(t => t.IsCurrent, ct);
        if (term is null) return Common.Error.NotFound("term.not_found", "That term doesn't exist.");

        var days = await db.Attendance.AsNoTracking()
            .Where(a => a.StudentId == q.StudentId && a.Date >= term.StartsOn && a.Date <= term.EndsOn)
            .OrderByDescending(a => a.Date).Select(a => new AttendanceDay(a.Date, a.Status, a.Note)).ToListAsync(ct);
        var present = days.Count(d => d.Status is AttendanceStatus.Present or AttendanceStatus.Late);
        return new ChildAttendance(q.StudentId, term.Id, term.DisplayName, days.Count, present, days.Count(d => d.Status == AttendanceStatus.Absent),
            days.Count(d => d.Status == AttendanceStatus.Late), days.Count(d => d.Status == AttendanceStatus.Excused),
            days.Count == 0 ? null : ReportMath.Round(100m * present / days.Count), days);
    }
}
