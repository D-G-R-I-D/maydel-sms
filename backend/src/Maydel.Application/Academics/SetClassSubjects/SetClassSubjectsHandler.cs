using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Academics;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Academics.SetClassSubjects;

internal sealed class SetClassSubjectsHandler(IAppDbContext db) : ICommandHandler<SetClassSubjectsCommand, IReadOnlyList<ClassSubjectDto>>
{
    public async Task<Result<IReadOnlyList<ClassSubjectDto>>> Handle(SetClassSubjectsCommand c, CancellationToken ct)
    {
        if (!await db.ClassRooms.AnyAsync(r => r.Id == c.ClassRoomId, ct)) return AcademicsErrors.ClassRoomNotFound;

        var existing = await db.ClassSubjects.Where(cs => cs.ClassRoomId == c.ClassRoomId).ToListAsync(ct);
        var wanted = c.Subjects.Select(s => s.SubjectId).ToHashSet();

        // New subjects must exist and be active; subjects already offered may stay even if since deactivated.
        var adding = wanted.Except(existing.Select(e => e.SubjectId)).ToList();
        var activeNew = await db.Subjects.CountAsync(s => adding.Contains(s.Id) && s.IsActive, ct);
        if (activeNew != adding.Count) return Error.Validation("subjects", "One or more subjects don't exist or are inactive.");

        var teacherIds = c.Subjects.Where(s => s.TeacherId is not null).Select(s => s.TeacherId!.Value).Distinct().ToList();
        var activeTeachers = await db.Staff.CountAsync(s => teacherIds.Contains(s.Id) && s.IsActive, ct);
        if (activeTeachers != teacherIds.Count) return Error.Validation("subjects", "Every assigned teacher must be an active staff member.");

        var removing = existing.Where(e => !wanted.Contains(e.SubjectId)).ToList();
        var removingIds = removing.Select(r => r.SubjectId).ToList();
        var withScores = await db.SubjectScores.Where(s => s.ClassRoomId == c.ClassRoomId && removingIds.Contains(s.SubjectId))
            .Select(s => s.SubjectId).Distinct().ToListAsync(ct);
        if (withScores.Count > 0)
        {
            var names = await db.Subjects.Where(s => withScores.Contains(s.Id)).OrderBy(s => s.Name).Select(s => s.Name).ToListAsync(ct);
            return Error.Rule("class_subjects.has_scores", $"Scores have been entered for {string.Join(", ", names)} in this class, so it can't be removed.");
        }

        db.ClassSubjects.RemoveRange(removing);
        foreach (var assignment in c.Subjects)
        {
            var row = existing.FirstOrDefault(e => e.SubjectId == assignment.SubjectId);
            if (row is null) db.ClassSubjects.Add(new ClassSubject { ClassRoomId = c.ClassRoomId, SubjectId = assignment.SubjectId, TeacherId = assignment.TeacherId });
            else if (row.TeacherId != assignment.TeacherId) row.TeacherId = assignment.TeacherId;
        }

        await db.SaveChangesAsync(ct);
        return await db.ClassSubjectDtos(c.ClassRoomId).ToListAsync(ct);
    }
}
