using Maydel.Application.Abstractions;
using Maydel.Domain.Academics;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Academics;

public sealed record TermDto(
    Guid Id, Guid SessionId, TermNumber Number, string Name, DateOnly StartsOn, DateOnly EndsOn,
    DateOnly? MidTermStartsOn, DateOnly? MidTermEndsOn, bool IsCurrent);

public sealed record SessionDto(Guid Id, string Name, DateOnly StartsOn, DateOnly EndsOn, bool IsCurrent, IReadOnlyList<TermDto> Terms);

public sealed record ClassLevelDto(Guid Id, string Name, SchoolSection Section, int Order);

public sealed record ClassRoomDto(
    Guid Id, Guid ClassLevelId, string ClassLevel, SchoolSection Section, int LevelOrder, string Arm, string Name,
    Guid? FormTeacherId, string? FormTeacher, int Capacity, int StudentCount);

public sealed record SubjectDto(Guid Id, string Name, string Code, bool IsActive);

public sealed record ClassSubjectDto(Guid Id, Guid SubjectId, string Subject, string Code, Guid? TeacherId, string? Teacher);

// Request bodies for endpoints whose id comes from the route.
public sealed record SessionRequest(string Name, DateOnly StartsOn, DateOnly EndsOn);
public sealed record TermRequest(TermNumber Number, DateOnly StartsOn, DateOnly EndsOn, DateOnly? MidTermStartsOn, DateOnly? MidTermEndsOn);
public sealed record TermDatesRequest(DateOnly StartsOn, DateOnly EndsOn, DateOnly? MidTermStartsOn, DateOnly? MidTermEndsOn);
public sealed record ClassRoomUpdateRequest(string Arm, int Capacity, Guid? FormTeacherId);
public sealed record SubjectRequest(string Name, string Code, bool IsActive = true);
public sealed record ClassSubjectAssignment(Guid SubjectId, Guid? TeacherId);
public sealed record ClassSubjectsRequest(IReadOnlyList<ClassSubjectAssignment> Subjects);

public static class AcademicsErrors
{
    public static readonly Common.Error SessionNotFound = Common.Error.NotFound("session.not_found", "That session doesn't exist.");
    public static readonly Common.Error TermNotFound = Common.Error.NotFound("term.not_found", "That term doesn't exist.");
    public static readonly Common.Error NoCurrentTerm = Common.Error.NotFound("term.none_current", "No current term is set. An administrator can set one under Sessions & terms.");
    public static readonly Common.Error ClassRoomNotFound = Common.Error.NotFound("classroom.not_found", "That class doesn't exist.");
    public static readonly Common.Error SubjectNotFound = Common.Error.NotFound("subject.not_found", "That subject doesn't exist.");
    public static Common.Error DuplicateSession(string name) => Common.Error.Conflict("session.duplicate", $"A session named {name} already exists.");
    public static Common.Error DuplicateClassRoom(string name) => Common.Error.Conflict("classroom.duplicate", $"{name} already exists.");
    public static readonly Common.Error DuplicateSubject = Common.Error.Conflict("subject.duplicate", "A subject with that name or code already exists.");
}

/// <summary>Read-model projections shared by this module's queries and command responses.</summary>
public static class AcademicsQueries
{
    public static IQueryable<TermDto> ToDtos(this IQueryable<Term> terms) => terms.Select(t => new TermDto(
        t.Id, t.SessionId, t.Number, t.Number.ToString() + " Term " + t.Session.Name, t.StartsOn, t.EndsOn,
        t.MidTermStartsOn, t.MidTermEndsOn, t.IsCurrent));

    public static async Task<SessionDto?> GetSessionAsync(this IAppDbContext db, Guid id, CancellationToken ct) =>
        (await db.ListSessionsAsync(id, ct)).SingleOrDefault();

    /// <summary>Sessions newest first with their terms in order; all of them, or just <paramref name="onlyId"/>.</summary>
    public static async Task<IReadOnlyList<SessionDto>> ListSessionsAsync(this IAppDbContext db, Guid? onlyId, CancellationToken ct)
    {
        var sessions = db.Sessions.AsNoTracking();
        if (onlyId is { } id) sessions = sessions.Where(s => s.Id == id);
        var list = await sessions.OrderByDescending(s => s.StartsOn)
            .Select(s => new { s.Id, s.Name, s.StartsOn, s.EndsOn, s.IsCurrent }).ToListAsync(ct);
        var ids = list.Select(s => s.Id).ToList();
        var terms = await db.Terms.AsNoTracking().Where(t => ids.Contains(t.SessionId)).OrderBy(t => t.Number).ToDtos().ToListAsync(ct);
        return [.. list.Select(s => new SessionDto(s.Id, s.Name, s.StartsOn, s.EndsOn, s.IsCurrent, [.. terms.Where(t => t.SessionId == s.Id)]))];
    }

    /// <summary>All classes, or just <paramref name="onlyId"/> (filter before projecting: EF can't filter on the DTO).</summary>
    public static IQueryable<ClassRoomDto> ClassRoomDtos(this IAppDbContext db, Guid? onlyId = null) =>
        db.ClassRooms.AsNoTracking()
            .Where(r => onlyId == null || r.Id == onlyId)
            .OrderBy(r => r.ClassLevel.Order).ThenBy(r => r.Arm)
            .Select(r => new ClassRoomDto(
                r.Id, r.ClassLevelId, r.ClassLevel.Name, r.ClassLevel.Section, r.ClassLevel.Order, r.Arm,
                r.ClassLevel.Name + " " + r.Arm, r.FormTeacherId, r.FormTeacher != null ? r.FormTeacher.FullName : null, r.Capacity,
                db.Students.Count(s => s.ClassRoomId == r.Id && s.Status == StudentStatus.Active)));

    public static IQueryable<ClassSubjectDto> ClassSubjectDtos(this IAppDbContext db, Guid classRoomId) =>
        db.ClassSubjects.AsNoTracking().Where(cs => cs.ClassRoomId == classRoomId).OrderBy(cs => cs.Subject.Name)
            .Select(cs => new ClassSubjectDto(cs.Id, cs.SubjectId, cs.Subject.Name, cs.Subject.Code, cs.TeacherId, cs.Teacher != null ? cs.Teacher.FullName : null));

    /// <summary>Staff who can be given classes: active staff records.</summary>
    public static Task<bool> IsActiveStaffAsync(this IAppDbContext db, Guid staffId, CancellationToken ct) =>
        db.Staff.AnyAsync(s => s.Id == staffId && s.IsActive, ct);
}
