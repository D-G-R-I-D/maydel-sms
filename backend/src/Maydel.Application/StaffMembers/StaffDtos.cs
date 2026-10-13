using Maydel.Application.Abstractions;
using Maydel.Application.Identity;
using Maydel.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.StaffMembers;

public sealed record StaffListItem(
    Guid Id, string StaffNumber, string FullName, string Email, string? Phone, string? JobTitle,
    IReadOnlyList<Role> Roles, bool IsActive, bool HasLogin, DateTimeOffset? LastLoginAt, string? PhotoVersion);

/// <summary>A class the staff member teaches. <c>SubjectId</c> null = they are its form teacher.</summary>
public sealed record StaffAssignment(Guid ClassRoomId, string ClassRoom, Guid? SubjectId, string? Subject);

public sealed record StaffDetail(
    Guid Id, string StaffNumber, string FullName, string Email, string? Phone, string? JobTitle,
    IReadOnlyList<Role> Roles, bool IsActive, bool HasLogin, bool MustChangePassword, DateTimeOffset? LastLoginAt,
    string? PhotoVersion, IReadOnlyList<StaffAssignment> Assignments);

/// <summary>For teacher pickers: active staff only.</summary>
public sealed record StaffLookupItem(Guid Id, string FullName, string? JobTitle);

/// <summary>
/// Result of creating a staff member. <c>Credentials</c> is the one and only time the temporary password is shown;
/// it is null when the email already had an account (e.g. a parent who joined the staff), which is linked instead.
/// </summary>
public sealed record StaffCreated(StaffDetail Staff, IssuedCredentials? Credentials, bool LinkedExistingAccount);

/// <summary>Body of <c>PUT /api/staff/{id}</c>.</summary>
public sealed record StaffUpdateRequest(string FullName, string Email, string? Phone, string? JobTitle, IReadOnlyList<Role> Roles, bool IsActive);

public static class StaffErrors
{
    public static readonly Common.Error NotFound = Common.Error.NotFound("staff.not_found", "That staff member doesn't exist.");
    public static readonly Common.Error DuplicateEmail = Common.Error.Conflict("staff.duplicate_email", "A staff member with that email already exists.");
    public static readonly Common.Error EmailInUse = Common.Error.Conflict("staff.email_in_use", "That email belongs to another account. Use a different email.");
    public static readonly Common.Error StudentAccount = Common.Error.Conflict("staff.email_is_student", "That email belongs to a student account.");
    public static readonly Common.Error SelfLockout = Common.Error.Rule("staff.self_lockout", "You can't remove your own Admin role or deactivate yourself. Ask another administrator.");
    public static readonly Common.Error SelfReset = Common.Error.Rule("staff.self_reset", "Use Change password to change your own password.");
    public static readonly Common.Error NoLogin = Common.Error.Rule("staff.no_login", "This staff member has no login.");
    public static readonly Common.Error Inactive = Common.Error.Rule("staff.inactive", "Reactivate this staff member before resetting their password.");
}

public static class StaffRoles
{
    public static readonly IReadOnlyList<Role> All = [Role.Admin, Role.Teacher, Role.Bursar];
    public static bool IsStaffRole(Role role) => All.Contains(role);
}

public static class StaffQueries
{
    /// <summary>Roles per user id (all roles, staff and others), in enum order.</summary>
    public static async Task<Dictionary<Guid, IReadOnlyList<Role>>> RolesByUserAsync(this IAppDbContext db, IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        var rows = await db.UserRoles.Where(ur => userIds.Contains(ur.UserId))
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.UserId).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<Role>)[.. g.Select(r => Enum.Parse<Role>(r.Name!)).Where(StaffRoles.IsStaffRole).Order()]);
    }

    public static async Task<StaffDetail?> GetStaffDetailAsync(this IAppDbContext db, Guid id, CancellationToken ct)
    {
        var staff = await db.Staff.AsNoTracking().Where(s => s.Id == id)
            .Select(s => new
            {
                s.Id, s.StaffNumber, s.FullName, s.Email, s.Phone, s.JobTitle, s.IsActive, s.UserId, s.PhotoPath,
                User = db.Users.Where(u => u.Id == s.UserId).Select(u => new { u.MustChangePassword, u.LastLoginAt }).FirstOrDefault(),
            })
            .FirstOrDefaultAsync(ct);
        if (staff is null) return null;

        var roles = staff.UserId is { } uid ? (await db.RolesByUserAsync([uid], ct)).GetValueOrDefault(uid, []) : [];
        var formClasses = await db.ClassRooms.AsNoTracking().Where(r => r.FormTeacherId == id)
            .Select(r => new StaffAssignment(r.Id, r.ClassLevel.Name + " " + r.Arm, null, null)).ToListAsync(ct);
        var subjects = await db.ClassSubjects.AsNoTracking().Where(cs => cs.TeacherId == id)
            .OrderBy(cs => cs.ClassRoom.ClassLevel.Order).ThenBy(cs => cs.Subject.Name)
            .Select(cs => new StaffAssignment(cs.ClassRoomId, cs.ClassRoom.ClassLevel.Name + " " + cs.ClassRoom.Arm, cs.SubjectId, cs.Subject.Name))
            .ToListAsync(ct);

        return new StaffDetail(
            staff.Id, staff.StaffNumber, staff.FullName, staff.Email, staff.Phone, staff.JobTitle, roles, staff.IsActive,
            staff.UserId is not null, staff.User?.MustChangePassword ?? false, staff.User?.LastLoginAt,
            Photos.PhotoStore.VersionOf(staff.PhotoPath), [.. formClasses, .. subjects]);
    }

    /// <summary>Next free number in the MSS-STF-001 sequence.</summary>
    public static async Task<string> NextStaffNumberAsync(this IAppDbContext db, CancellationToken ct)
    {
        const string prefix = "MSS-STF-";
        var numbers = await db.Staff.Where(s => s.StaffNumber.StartsWith(prefix)).Select(s => s.StaffNumber).ToListAsync(ct);
        var max = numbers.Select(n => int.TryParse(n[prefix.Length..], out var v) ? v : 0).DefaultIfEmpty(0).Max();
        return $"{prefix}{max + 1:000}";
    }
}
