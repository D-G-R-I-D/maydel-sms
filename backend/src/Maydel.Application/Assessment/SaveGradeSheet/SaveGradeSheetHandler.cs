using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Common.Access;
using Maydel.Domain.Assessment;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Assessment.SaveGradeSheet;

internal sealed class SaveGradeSheetHandler(IAppDbContext db, AccessRules access, ICurrentUser me) : ICommandHandler<SaveGradeSheetCommand, GradeSheet>
{
    public async Task<Result<GradeSheet>> Handle(SaveGradeSheetCommand c, CancellationToken ct)
    {
        if (!await access.CanGradeAsync(c.ClassRoomId, c.SubjectId, ct)) return AssessmentErrors.NotGrader;
        if (!await db.ClassSubjects.AnyAsync(cs => cs.ClassRoomId == c.ClassRoomId && cs.SubjectId == c.SubjectId, ct)) return AssessmentErrors.NotOffered;
        if (!await db.Terms.AnyAsync(t => t.Id == c.TermId, ct)) return AssessmentErrors.TermNotFound;

        var components = (await db.ActiveComponentsAsync(ct)).ToDictionary(x => x.Id);
        var students = await db.Students.Where(s => s.ClassRoomId == c.ClassRoomId && s.Status == StudentStatus.Active)
            .Select(s => new { s.Id, Name = s.FirstName + " " + s.LastName }).ToDictionaryAsync(s => s.Id, ct);

        // Validate every row before writing anything; errors keyed so the grid can mark the exact cell.
        var errors = new Dictionary<string, string[]>();
        for (var i = 0; i < c.Entries.Count; i++)
        {
            var entry = c.Entries[i];
            if (!students.ContainsKey(entry.StudentId)) { errors[$"entries[{i}].studentId"] = ["This student isn't an active member of the class."]; continue; }
            foreach (var value in entry.Scores)
            {
                if (!components.TryGetValue(value.ComponentId, out var component)) errors[$"entries[{i}].scores"] = ["Unknown or inactive score component. Reload the sheet."];
                else if (value.Score is { } score && AssessmentRules.CheckScore(score, component) is { } message) errors[$"entries[{i}].scores.{component.Id}"] = [message];
            }
        }
        if (errors.Count > 0) return new ValidationError(errors);

        var published = await db.PublishedStudentsAsync(c.ClassRoomId, c.TermId, ct);
        var lockedNames = c.Entries.Where(e => published.Contains(e.StudentId)).Select(e => students[e.StudentId].Name).ToList();
        if (lockedNames.Count > 0) return AssessmentErrors.Published(lockedNames);

        var ids = c.Entries.Select(e => e.StudentId).ToList();
        var existing = await db.SubjectScores
            .Where(s => s.ClassRoomId == c.ClassRoomId && s.SubjectId == c.SubjectId && s.TermId == c.TermId && ids.Contains(s.StudentId))
            .ToDictionaryAsync(s => s.StudentId, ct);
        var scale = await db.GradingScaleAsync(ct);

        foreach (var entry in c.Entries)
        {
            var entered = entry.Scores.Where(v => v.Score is not null).Select(v => (v.ComponentId, v.Score!.Value)).ToList();
            var comment = string.IsNullOrWhiteSpace(entry.Comment) ? null : entry.Comment.Trim();
            existing.TryGetValue(entry.StudentId, out var score);

            if (entered.Count == 0 && comment is null)
            {
                if (score is not null) db.SubjectScores.Remove(score);
                continue;
            }
            if (score is null)
            {
                score = new SubjectScore { StudentId = entry.StudentId, ClassRoomId = c.ClassRoomId, SubjectId = c.SubjectId, TermId = c.TermId };
                db.SubjectScores.Add(score);
            }
            score.SetScores(entered, scale);
            score.TeacherComment = comment;
            score.EnteredByUserId = me.UserId;
        }

        await db.SaveChangesAsync(ct);
        return await GradeSheetReader.ReadAsync(db, c.ClassRoomId, c.SubjectId, c.TermId, ct);
    }
}
