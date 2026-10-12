using Maydel.Domain.Assessment;
using Maydel.Domain.Attendance;
using Maydel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Api.Features;

public sealed record ComponentHeader(Guid Id, string Name, decimal MaxScore);
public sealed record ReportSubjectLine(
    string Subject, IReadOnlyList<decimal?> Scores, decimal Total, string? Grade, string? Remark, string? Comment,
    decimal ClassHighest, decimal ClassLowest, decimal ClassAverage);
public sealed record GradeKey(string Grade, decimal MinScore, decimal MaxScore, string Remark);

public sealed record ReportCardDto(
    Guid StudentId, string StudentName, string AdmissionNumber, bool HasPhoto, string? House, string? Sex,
    Guid TermId, string Term, string Session, string ClassRoom, DateOnly TermEndsOn,
    IReadOnlyList<ComponentHeader> Components, IReadOnlyList<ReportSubjectLine> Subjects,
    decimal Average, decimal ClassAverage, int? Position, int ClassSize,
    int DaysOpened, int DaysPresent, int DaysAbsent,
    string? FormTeacherComment, string? FormTeacher, string? PrincipalComment,
    ReportStatus Status, DateTimeOffset? PublishedAt, IReadOnlyList<GradeKey> GradingKey);

/// <summary>
/// Builds report cards and class-level statistics (averages, positions, attendance) for a class and term.
/// Publishing freezes these numbers onto <see cref="TermReport"/> rows.
/// </summary>
public sealed class ReportCalculator(AppDbContext db, TimeProvider clock)
{
    public async Task<int> PublishAsync(Guid classRoomId, Guid termId, Guid publishedBy, CancellationToken ct)
    {
        var stats = await ComputeClassAsync(classRoomId, termId, ct);
        if (stats.Averages.Count == 0) throw new Domain.Common.DomainException("No scores have been entered for this class and term.");

        var reports = await db.TermReports.Where(r => r.ClassRoomId == classRoomId && r.TermId == termId).ToDictionaryAsync(r => r.StudentId, ct);
        var positions = GradingScale.Rank(stats.Averages);
        var now = clock.GetUtcNow();

        foreach (var (studentId, average) in stats.Averages)
        {
            if (!reports.TryGetValue(studentId, out var report))
            {
                report = new TermReport { StudentId = studentId, TermId = termId, ClassRoomId = classRoomId };
                db.TermReports.Add(report);
            }
            report.Average = average;
            report.ClassAverage = stats.ClassAverage;
            report.Position = positions[studentId];
            report.ClassSize = stats.Averages.Count;
            report.DaysOpened = stats.DaysOpened;
            report.DaysPresent = stats.Present.GetValueOrDefault(studentId);
            report.Status = ReportStatus.Published;
            report.PublishedAt = now;
            report.PublishedByUserId = publishedBy;
        }
        await db.SaveChangesAsync(ct);
        return stats.Averages.Count;
    }

    public async Task<ReportCardDto?> BuildAsync(Guid studentId, Guid termId, bool publishedOnly, CancellationToken ct)
    {
        var report = await db.TermReports.AsNoTracking().FirstOrDefaultAsync(r => r.StudentId == studentId && r.TermId == termId, ct);
        if (publishedOnly && report?.Status != ReportStatus.Published) return null;

        var student = await db.Students.AsNoTracking().Include(s => s.ClassRoom!).ThenInclude(c => c.ClassLevel)
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);
        var term = await db.Terms.AsNoTracking().Include(t => t.Session).FirstOrDefaultAsync(t => t.Id == termId, ct);
        if (student is null || term is null) return null;

        // A published report belongs to the class the student was in at the time, not their current class.
        var classRoomId = report?.ClassRoomId ?? student.ClassRoomId;
        if (classRoomId is null) return null;
        var classRoom = await db.ClassRooms.AsNoTracking().Include(c => c.ClassLevel).Include(c => c.FormTeacher)
            .FirstAsync(c => c.Id == classRoomId, ct);

        var components = await db.AssessmentComponents.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Order)
            .Select(c => new ComponentHeader(c.Id, c.Name, c.MaxScore)).ToListAsync(ct);
        var bands = await db.GradeBands.AsNoTracking().OrderByDescending(b => b.MinScore).ToListAsync(ct);

        var classScores = await db.SubjectScores.AsNoTracking().Include(s => s.Components).Include(s => s.Subject)
            .Where(s => s.ClassRoomId == classRoomId && s.TermId == termId).ToListAsync(ct);
        var subjectStats = classScores.GroupBy(s => s.SubjectId).ToDictionary(g => g.Key, g =>
            (High: g.Max(x => x.Total), Low: g.Min(x => x.Total), Avg: Math.Round(g.Average(x => x.Total), 2)));

        var lines = classScores.Where(s => s.StudentId == studentId).OrderBy(s => s.Subject.Name).Select(s =>
        {
            var st = subjectStats[s.SubjectId];
            var scores = components.Select(c => s.Components.FirstOrDefault(x => x.ComponentId == c.Id)?.Score).ToList();
            return new ReportSubjectLine(s.Subject.Name, scores, s.Total, s.Grade, s.Remark, s.TeacherComment, st.High, st.Low, st.Avg);
        }).ToList();

        decimal average; decimal classAverage; int? position; int classSize; int daysOpened; int daysPresent;
        if (report?.Status == ReportStatus.Published)
        {
            (average, classAverage, position, classSize, daysOpened, daysPresent) =
                (report.Average, report.ClassAverage, report.Position, report.ClassSize, report.DaysOpened, report.DaysPresent);
        }
        else
        {
            // Draft preview: live numbers.
            var stats = await ComputeClassAsync(classRoomId.Value, termId, ct);
            average = stats.Averages.GetValueOrDefault(studentId);
            classAverage = stats.ClassAverage;
            position = stats.Averages.ContainsKey(studentId) ? GradingScale.Rank(stats.Averages)[studentId] : null;
            classSize = stats.Averages.Count;
            daysOpened = stats.DaysOpened;
            daysPresent = stats.Present.GetValueOrDefault(studentId);
        }

        return new ReportCardDto(
            student.Id, student.FullName, student.AdmissionNumber, student.PhotoPath is not null, student.House, student.Sex?.ToString(),
            term.Id, $"{term.Number} Term", term.Session.Name, classRoom.DisplayName, term.EndsOn,
            components, lines, average, classAverage, position, classSize,
            daysOpened, daysPresent, Math.Max(0, daysOpened - daysPresent),
            report?.FormTeacherComment, classRoom.FormTeacher?.FullName, report?.PrincipalComment,
            report?.Status ?? ReportStatus.Draft, report?.PublishedAt,
            bands.Select(b => new GradeKey(b.Grade, b.MinScore, b.MaxScore, b.Remark)).ToList());
    }

    private sealed record ClassStats(Dictionary<Guid, decimal> Averages, decimal ClassAverage, int DaysOpened, Dictionary<Guid, int> Present);

    private async Task<ClassStats> ComputeClassAsync(Guid classRoomId, Guid termId, CancellationToken ct)
    {
        var totals = await db.SubjectScores.AsNoTracking()
            .Where(s => s.ClassRoomId == classRoomId && s.TermId == termId)
            .GroupBy(s => s.StudentId)
            .Select(g => new { StudentId = g.Key, Average = g.Average(x => x.Total) })
            .ToListAsync(ct);
        var averages = totals.ToDictionary(t => t.StudentId, t => Math.Round(t.Average, 2));
        var classAverage = averages.Count == 0 ? 0 : Math.Round(averages.Values.Average(), 2);

        var term = await db.Terms.AsNoTracking().FirstAsync(t => t.Id == termId, ct);
        var register = db.Attendance.AsNoTracking()
            .Where(a => a.ClassRoomId == classRoomId && a.Date >= term.StartsOn && a.Date <= term.EndsOn);
        var daysOpened = await register.Select(a => a.Date).Distinct().CountAsync(ct);
        var present = await register
            .Where(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late)
            .GroupBy(a => a.StudentId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return new ClassStats(averages, classAverage, daysOpened, present);
    }
}
