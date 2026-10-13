using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Assessment;

internal static class GradeSheetReader
{
    public static async Task<Result<GradeSheet>> ReadAsync(IAppDbContext db, Guid classRoomId, Guid subjectId, Guid termId, CancellationToken ct)
    {
        var head = await db.ClassSubjects.AsNoTracking().Where(cs => cs.ClassRoomId == classRoomId && cs.SubjectId == subjectId)
            .Select(cs => new { ClassRoom = cs.ClassRoom.ClassLevel.Name + " " + cs.ClassRoom.Arm, Subject = cs.Subject.Name })
            .FirstOrDefaultAsync(ct);
        if (head is null) return AssessmentErrors.NotOffered;
        var term = await db.Terms.AsNoTracking().Where(t => t.Id == termId)
            .Select(t => t.Number.ToString() + " Term " + t.Session.Name).FirstOrDefaultAsync(ct);
        if (term is null) return AssessmentErrors.TermNotFound;

        var components = await db.ActiveComponentsAsync(ct);
        var students = await db.Students.AsNoTracking()
            .Where(s => s.ClassRoomId == classRoomId && s.Status == StudentStatus.Active)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Select(s => new { s.Id, s.AdmissionNumber, Name = s.LastName + ", " + s.FirstName }).ToListAsync(ct);
        var scores = await db.SubjectScores.AsNoTracking()
            .Where(s => s.ClassRoomId == classRoomId && s.SubjectId == subjectId && s.TermId == termId)
            .ToDictionaryAsync(s => s.StudentId, ct);
        var locked = await db.PublishedStudentsAsync(classRoomId, termId, ct);

        return new GradeSheet(classRoomId, head.ClassRoom, subjectId, head.Subject, termId, term,
            [.. components.Select(c => new GradeSheetComponent(c.Id, c.Name, c.MaxScore))],
            [.. students.Select(s =>
            {
                scores.TryGetValue(s.Id, out var score);
                return new GradeSheetRow(s.Id, s.AdmissionNumber, s.Name,
                    [.. components.Select(c => new ComponentScoreValue(c.Id, score?.Components.FirstOrDefault(x => x.ComponentId == c.Id)?.Score))],
                    score?.Total, score?.Grade, score?.Remark, score?.TeacherComment, locked.Contains(s.Id));
            })]);
    }
}
