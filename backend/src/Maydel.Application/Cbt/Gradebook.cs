using Maydel.Application.Abstractions;
using Maydel.Application.Assessment;
using Maydel.Application.Common.Access;
using Maydel.Domain.Assessment;
using Maydel.Domain.Cbt;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt;

/// <summary>Scaling released test scores into a grade sheet column (E16.5). Never touches a published report.</summary>
public sealed class Gradebook(IAppDbContext db, Results results, AccessRules access)
{
    public async Task<(GradebookPreview Preview, AssessmentComponent Component)?> PreviewAsync(CbtTest test, CancellationToken ct)
    {
        if (test.ComponentId is not { } componentId) return null;
        var component = await db.AssessmentComponents.AsNoTracking().FirstAsync(c => c.Id == componentId, ct);
        var attempts = await results.AttemptsAsync(test.Id, ct);
        var best = attempts.GroupBy(a => a.StudentId).Select(g => Attempts.Best(g)).OfType<TestAttempt>().ToDictionary(a => a.StudentId);
        var students = await db.Students.AsNoTracking().Where(s => best.Keys.Contains(s.Id))
            .Select(s => new { s.Id, Name = s.FirstName + " " + s.LastName, s.Status, s.ClassRoomId, Room = s.ClassRoom == null ? null : s.ClassRoom.ClassLevel.Name + " " + s.ClassRoom.Arm })
            .ToListAsync(ct);
        var current = (await db.SubjectScores.AsNoTracking()
                .Where(x => x.TermId == test.TermId && x.SubjectId == test.SubjectId && best.Keys.Contains(x.StudentId))
                .Select(x => new { x.StudentId, x.ClassRoomId, Score = x.Components.Where(cs => cs.ComponentId == componentId).Select(cs => (decimal?)cs.Score).FirstOrDefault() })
                .ToListAsync(ct))
            .GroupBy(x => x.StudentId).ToDictionary(g => g.Key, g => g.ToList());

        var published = new Dictionary<Guid, HashSet<Guid>>();
        var grader = new Dictionary<Guid, bool>();
        var rows = new List<GradebookRow>();
        foreach (var s in students.OrderBy(s => s.Room).ThenBy(s => s.Name))
        {
            var raw = best[s.Id].TotalScore!.Value;
            var scaled = CbtTest.Scale(raw, test.TotalMarks, component.MaxScore);
            PushState state;
            decimal? now = null;
            if (s.Status != Domain.People.StudentStatus.Active || s.ClassRoomId is not { } room || !test.ClassRoomIds.Contains(room))
            {
                state = PushState.NotInClass;
            }
            else
            {
                now = current.GetValueOrDefault(s.Id)?.FirstOrDefault(x => x.ClassRoomId == room)?.Score;
                if (!published.TryGetValue(room, out var locked)) published[room] = locked = await db.PublishedStudentsAsync(room, test.TermId, ct);
                if (!grader.TryGetValue(room, out var may)) grader[room] = may = await access.CanGradeAsync(room, test.SubjectId, ct);
                state = locked.Contains(s.Id) ? PushState.ReportPublished : may ? PushState.Ready : PushState.NotYourClass;
            }
            rows.Add(new GradebookRow(s.Id, s.Name, s.Room, raw, scaled, now, state));
        }
        return (new GradebookPreview(test.Id, component.Name, component.MaxScore, test.TotalMarks, rows), component);
    }

    /// <summary>Writes the scaled score into the component for each ready student (other components untouched); returns the skipped rows.</summary>
    public async Task<(int Pushed, List<GradebookRow> Skipped)> PushAsync(CbtTest test, GradebookPreview preview, AssessmentComponent component,
        IReadOnlyCollection<Guid>? only, Guid? by, CancellationToken ct)
    {
        var chosen = preview.Rows.Where(r => only is null || only.Contains(r.StudentId)).ToList();
        var ready = chosen.Where(r => r.State == PushState.Ready).ToList();
        var ids = ready.Select(r => r.StudentId).ToList();
        var rooms = await db.Students.Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.ClassRoomId!.Value, ct);
        var existing = await db.SubjectScores.Where(x => x.TermId == test.TermId && x.SubjectId == test.SubjectId && ids.Contains(x.StudentId)).ToListAsync(ct);
        var scale = await db.GradingScaleAsync(ct);
        foreach (var row in ready)
        {
            var room = rooms[row.StudentId];
            var score = existing.FirstOrDefault(x => x.StudentId == row.StudentId && x.ClassRoomId == room);
            if (score is null)
            {
                score = new SubjectScore { StudentId = row.StudentId, ClassRoomId = room, SubjectId = test.SubjectId, TermId = test.TermId, EnteredByUserId = by };
                db.SubjectScores.Add(score);
            }
            var others = score.Components.Where(c => c.ComponentId != component.Id).Select(c => (c.ComponentId, c.Score)).ToList();
            score.SetScores([.. others, (component.Id, Math.Min(row.Scaled, component.MaxScore))], scale);
        }
        return (ready.Count, [.. chosen.Where(r => r.State != PushState.Ready)]);
    }
}
