using Maydel.Application.Abstractions;
using Maydel.Domain.Common;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Common.Access;

/// <summary>
/// Row-level access rules shared by every module (docs/04-security.md §3). Role policies on endpoints decide
/// *what kind* of data a caller may touch; these rules decide *which rows*. Handlers must call them for
/// anything student-scoped.
/// </summary>
public sealed class AccessRules(IAppDbContext db, ICurrentUser me)
{
    private bool IsStaff => me.IsInRole(Roles.Admin) || me.IsInRole(Roles.Teacher) || me.IsInRole(Roles.Bursar);

    /// <summary>Staff see all students; parents only their linked children; students only themselves.</summary>
    public async Task<bool> CanViewStudentAsync(Guid studentId, CancellationToken ct)
    {
        if (IsStaff) return true;
        if (me.UserId is not { } uid) return false;
        return await PortalStudents(uid).AnyAsync(s => s.Id == studentId, ct);
    }

    /// <summary>Active students the signed-in parent or student may see in the portal. Empty for staff and anonymous callers.</summary>
    public IQueryable<Student> PortalStudents() =>
        me.UserId is { } uid && (me.IsInRole(Roles.Parent) || me.IsInRole(Roles.Student))
            ? PortalStudents(uid)
            : db.Students.Where(_ => false);

    /// <summary>Admins grade anything; teachers only subjects they teach, or any subject in a class they form-teach.</summary>
    public async Task<bool> CanGradeAsync(Guid classRoomId, Guid subjectId, CancellationToken ct)
    {
        if (me.IsInRole(Roles.Admin)) return true;
        if (!me.IsInRole(Roles.Teacher) || me.UserId is not { } uid) return false;
        return await db.ClassSubjects.AnyAsync(cs => cs.ClassRoomId == classRoomId && cs.SubjectId == subjectId &&
            ((cs.Teacher != null && cs.Teacher.UserId == uid) || (cs.ClassRoom.FormTeacher != null && cs.ClassRoom.FormTeacher.UserId == uid)), ct);
    }

    /// <summary>Admins, or the class's form teacher.</summary>
    public async Task<bool> IsFormTeacherAsync(Guid classRoomId, CancellationToken ct)
    {
        if (me.IsInRole(Roles.Admin)) return true;
        if (!me.IsInRole(Roles.Teacher) || me.UserId is not { } uid) return false;
        return await db.ClassRooms.AnyAsync(c => c.Id == classRoomId && c.FormTeacher != null && c.FormTeacher.UserId == uid, ct);
    }

    private IQueryable<Student> PortalStudents(Guid uid)
    {
        var asStudent = me.IsInRole(Roles.Student);
        var asParent = me.IsInRole(Roles.Parent);
        // Suspended, withdrawn and graduated students drop out of the portal (E3.5); their records are kept.
        return db.Students.Where(s => s.Status == StudentStatus.Active && (
            (asStudent && s.UserId == uid) ||
            (asParent && s.Guardians.Any(g => g.Guardian.UserId == uid))));
    }
}
