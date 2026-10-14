using Maydel.Application.Abstractions;
using Maydel.Application.Assessment;
using Maydel.Domain.Assessment;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Reports;

/// <summary>Computes class figures (averages, positions, subject statistics) and assembles report cards.</summary>
public sealed class ReportBuilder(IAppDbContext db)
{
    public sealed record ClassFigures(
        Dictionary<Guid, decimal> Averages, Dictionary<Guid, int> Positions, decimal ClassAverage,
        Dictionary<Guid, ReportMath.SubjectStats> SubjectStats, Dictionary<Guid, int> SubjectsScored);

    /// <summary>
    /// Live figures for the class: every active student currently in it with at least one score this term.
    /// Average = mean of their subject totals; positions use competition ranking (1, 2, 2, 4).
    /// </summary>
    public async Task<ClassFigures> ComputeAsync(Guid classRoomId, Guid termId, CancellationToken ct)
    {
        var activeIds = await db.Students.Where(s => s.ClassRoomId == classRoomId && s.Status == StudentStatus.Active).Select(s => s.Id).ToListAsync(ct);
        var totals = await db.SubjectScores.AsNoTracking()
            .Where(s => s.ClassRoomId == classRoomId && s.TermId == termId && activeIds.Contains(s.StudentId))
            .Select(s => new { s.StudentId, s.SubjectId, s.Total }).ToListAsync(ct);

        var averages = totals.GroupBy(t => t.StudentId)
            .ToDictionary(g => g.Key, g => ReportMath.Average([.. g.Select(x => x.Total)])!.Value);
        return new ClassFigures(
            averages,
            GradingScale.Rank(averages),
            ReportMath.ClassAverage(averages.Values),
            totals.GroupBy(t => t.SubjectId).ToDictionary(g => g.Key, g => ReportMath.ForSubject([.. g.Select(x => x.Total)])),
            totals.GroupBy(t => t.StudentId).ToDictionary(g => g.Key, g => g.Count()));
    }

    /// <summary>The class a student's report for this term belongs to: the published/draft report's class, else their current class.</summary>
    public async Task<Guid?> ReportClassAsync(Guid studentId, Guid termId, CancellationToken ct) =>
        await db.TermReports.Where(r => r.StudentId == studentId && r.TermId == termId).Select(r => (Guid?)r.ClassRoomId).FirstOrDefaultAsync(ct)
        ?? await db.Students.Where(s => s.Id == studentId).Select(s => s.ClassRoomId).FirstOrDefaultAsync(ct);

    public async Task<ReportCard?> BuildAsync(Guid studentId, Guid termId, Guid classRoomId, CancellationToken ct)
    {
        var student = await db.Students.AsNoTracking().Where(s => s.Id == studentId)
            .Select(s => new { s.Id, s.FirstName, s.MiddleName, s.LastName, s.AdmissionNumber, s.Sex, s.DateOfBirth, s.House, s.PhotoPath })
            .FirstOrDefaultAsync(ct);
        var term = await db.Terms.AsNoTracking().Where(t => t.Id == termId)
            .Select(t => new { t.Id, t.Number, t.EndsOn, t.StartsOn, t.SessionId, Session = t.Session.Name, SessionStarts = t.Session.StartsOn })
            .FirstOrDefaultAsync(ct);
        var room = await db.ClassRooms.AsNoTracking().Where(r => r.Id == classRoomId)
            .Select(r => new { Name = r.ClassLevel.Name + " " + r.Arm, FormTeacher = r.FormTeacher != null ? r.FormTeacher.FullName : null })
            .FirstOrDefaultAsync(ct);
        if (student is null || term is null || room is null) return null;

        var report = await db.TermReports.AsNoTracking().FirstOrDefaultAsync(r => r.StudentId == studentId && r.TermId == termId, ct);
        var classScores = await db.SubjectScores.AsNoTracking().Include(s => s.Subject)
            .Where(s => s.ClassRoomId == classRoomId && s.TermId == termId).ToListAsync(ct);

        // Columns: active components, plus any (since deactivated) component this class was actually scored on.
        var usedIds = classScores.SelectMany(s => s.Components).Select(c => c.ComponentId).Distinct().ToList();
        var components = await db.AssessmentComponents.AsNoTracking().Where(c => c.IsActive || usedIds.Contains(c.Id))
            .OrderBy(c => c.Order).Select(c => new GradeSheetComponent(c.Id, c.Name, c.MaxScore)).ToListAsync(ct);

        // Term comparison: only *published* earlier terms of the same session, so drafts never leak onto a report.
        var earlier = await db.TermReports.AsNoTracking()
            .Where(r => r.StudentId == studentId && r.Status == ReportStatus.Published && r.Term.SessionId == term.SessionId && r.Term.Number < term.Number)
            .OrderBy(r => r.Term.Number)
            .Select(r => new PreviousTermResult(r.TermId, r.Term.Number, r.Term.Number.ToString() + " Term", r.Average, r.Position, r.ClassSize))
            .ToListAsync(ct);
        var earlierIds = earlier.Select(e => e.TermId).ToList();
        var earlierScores = await db.SubjectScores.AsNoTracking()
            .Where(s => s.StudentId == studentId && earlierIds.Contains(s.TermId))
            .Select(s => new { s.SubjectId, s.Total, s.Grade, s.Term.Number }).ToListAsync(ct);
        var lastTerm = earlier.LastOrDefault();

        var subjectStats = classScores.GroupBy(s => s.SubjectId).ToDictionary(g => g.Key, g => ReportMath.ForSubject([.. g.Select(x => x.Total)]));
        var lines = classScores.Where(s => s.StudentId == studentId).OrderBy(s => s.Subject.Name).Select(s =>
        {
            var stats = subjectStats[s.SubjectId];
            var previous = earlierScores.Where(p => p.SubjectId == s.SubjectId).OrderBy(p => p.Number)
                .Select(p => new PreviousSubjectResult(p.Number, p.Number + " Term", p.Total, p.Grade)).ToList();
            var last = lastTerm is null ? null : previous.FirstOrDefault(p => p.Number == lastTerm.Number);
            return new ReportSubjectLine(s.SubjectId, s.Subject.Name,
                [.. components.Select(c => new ComponentScoreValue(c.Id, s.Components.FirstOrDefault(x => x.ComponentId == c.Id)?.Score))],
                s.Total, s.Grade, s.Remark, s.TeacherComment, stats.Highest, stats.Lowest, stats.Average,
                previous, last is null ? null : s.Total - last.Total, last is null ? null : ReportMath.TrendOf(s.Total, last.Total));
        }).ToList();

        decimal? average; decimal classAverage; int? position; int classSize;
        if (report is { IsPublished: true })
        {
            (average, classAverage, position, classSize) = (report.Average, report.ClassAverage, report.Position, report.ClassSize);
        }
        else
        {
            var live = await ComputeAsync(classRoomId, termId, ct);
            average = live.Averages.TryGetValue(studentId, out var a) ? a : null;
            classAverage = live.ClassAverage;
            position = live.Positions.TryGetValue(studentId, out var p) ? p : null;
            classSize = live.Averages.Count;
        }

        var nextTerm = await db.Terms.AsNoTracking()
            .Where(t => (t.SessionId == term.SessionId && t.Number > term.Number) || t.Session.StartsOn > term.SessionStarts)
            .OrderBy(t => t.StartsOn).Select(t => (DateOnly?)t.StartsOn).FirstOrDefaultAsync(ct);
        var bands = await db.GradeBands.AsNoTracking().OrderByDescending(b => b.MinScore)
            .Select(b => new GradeBandDto(b.Grade, b.MinScore, b.MaxScore, b.Remark)).ToListAsync(ct);

        // Behaviour (E2.5): active traits, plus any since-deactivated trait this report rated.
        var rated = report?.TraitRatings ?? [];
        var ratedIds = rated.Select(r => r.TraitId).ToList();
        var traits = await db.BehaviourTraits.AsNoTracking().Where(t => t.IsActive || ratedIds.Contains(t.Id))
            .OrderBy(t => t.Order).Select(t => new { t.Id, t.Name }).ToListAsync(ct);
        var behaviour = traits.Select(t => new TraitRatingLine(t.Id, t.Name, rated.FirstOrDefault(r => r.TraitId == t.Id)?.Rating)).ToList();
        var clubs = (report?.Clubs ?? []).OrderBy(c => c.Order).Select(c => new ClubLine(c.Name, c.Rating)).ToList();

        var name = string.Join(' ', new[] { student.FirstName, student.MiddleName, student.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
        decimal? averageChange = average is { } now && lastTerm is not null ? now - lastTerm.Average : null;
        Trend? trend = average is { } current && lastTerm is not null ? ReportMath.TrendOf(current, lastTerm.Average) : null;
        decimal? cumulative = average is { } thisTerm ? ReportMath.Cumulative([.. earlier.Select(e => e.Average), thisTerm]) : null;
        var hasAttendance = report is not null && report.DaysOpened > 0;
        return new ReportCard(
            student.Id, name, student.AdmissionNumber, student.Sex, student.DateOfBirth, student.House, Photos.PhotoStore.VersionOf(student.PhotoPath),
            classRoomId, room.Name, term.Id, $"{term.Number} Term", term.Session, term.EndsOn, nextTerm,
            report?.Status ?? ReportStatus.Draft, report?.PublishedAt, components, lines,
            average, classAverage, position, classSize, lines.Sum(l => l.Total),
            earlier, averageChange, trend, cumulative,
            hasAttendance ? report!.DaysOpened : null, hasAttendance ? report!.DaysPresent : null, hasAttendance ? report!.DaysAbsent : null,
            room.FormTeacher, report?.FormTeacherComment, report?.PrincipalComment, bands,
            behaviour, clubs, [.. BehaviourRatings.Key.Select(k => new RatingKeyEntry(k.Rating, k.Meaning))]);
    }

    /// <summary>The report row for a student and term, created as a draft if missing.</summary>
    public async Task<TermReport> GetOrCreateAsync(Guid studentId, Guid termId, Guid classRoomId, CancellationToken ct)
    {
        var report = await db.TermReports.FirstOrDefaultAsync(r => r.StudentId == studentId && r.TermId == termId, ct);
        if (report is null)
        {
            report = new TermReport { StudentId = studentId, TermId = termId, ClassRoomId = classRoomId };
            db.TermReports.Add(report);
        }
        return report;
    }
}
