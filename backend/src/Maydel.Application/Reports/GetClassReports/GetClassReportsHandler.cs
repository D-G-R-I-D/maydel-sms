using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Common.Access;
using Maydel.Domain.Assessment;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Reports.GetClassReports;

internal sealed class GetClassReportsHandler(IAppDbContext db, AccessRules access, ReportBuilder reports) : IQueryHandler<GetClassReportsQuery, ClassReportSummary>
{
    public async Task<Result<ClassReportSummary>> Handle(GetClassReportsQuery q, CancellationToken ct)
    {
        if (!await access.TeachesClassAsync(q.ClassRoomId, ct)) return ReportErrors.NotAllowed;
        var room = await db.ClassRooms.AsNoTracking().Where(r => r.Id == q.ClassRoomId)
            .Select(r => new { Name = r.ClassLevel.Name + " " + r.Arm, FormTeacher = r.FormTeacher != null ? r.FormTeacher.FullName : null, Offered = r.Subjects.Count })
            .FirstOrDefaultAsync(ct);
        if (room is null) return ReportErrors.ClassNotFound;
        var term = await db.Terms.AsNoTracking().Where(t => t.Id == q.TermId).Select(t => t.Number.ToString() + " Term " + t.Session.Name).FirstOrDefaultAsync(ct);
        if (term is null) return ReportErrors.TermNotFound;

        var students = await db.Students.AsNoTracking().Where(s => s.ClassRoomId == q.ClassRoomId && s.Status == StudentStatus.Active)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Select(s => new { s.Id, s.AdmissionNumber, Name = s.LastName + ", " + s.FirstName }).ToListAsync(ct);
        var rows = await db.TermReports.AsNoTracking().Where(r => r.ClassRoomId == q.ClassRoomId && r.TermId == q.TermId).ToDictionaryAsync(r => r.StudentId, ct);
        var live = await reports.ComputeAsync(q.ClassRoomId, q.TermId, ct);

        var lines = students.Select(s =>
        {
            rows.TryGetValue(s.Id, out var r);
            var published = r is { IsPublished: true };
            return new ClassReportRow(s.Id, s.AdmissionNumber, s.Name, live.SubjectsScored.GetValueOrDefault(s.Id),
                published ? r!.Average : live.Averages.TryGetValue(s.Id, out var a) ? a : null,
                published ? r!.Position : live.Positions.TryGetValue(s.Id, out var p) ? p : null,
                r?.Status ?? ReportStatus.Draft, r is { DaysOpened: > 0 } ? r.DaysPresent : null,
                r?.FormTeacherComment is not null, r?.PrincipalComment is not null, r?.TraitRatings.Count ?? 0);
        }).ToList();

        var daysOpened = rows.Values.Where(r => r.DaysOpened > 0).Select(r => (int?)r.DaysOpened).Max();
        return new ClassReportSummary(q.ClassRoomId, room.Name, q.TermId, term, room.FormTeacher, room.Offered, live.ClassAverage, daysOpened,
            lines.Count(l => l.Status == ReportStatus.Published), lines.Count(l => l.Status == ReportStatus.Draft), lines);
    }
}
