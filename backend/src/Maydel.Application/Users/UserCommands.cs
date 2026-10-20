using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Identity;
using Maydel.Domain.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Users;

public enum AccountKind
{
    Staff,
    Parent,
    Student,
    Applicant,
    Other,
}

/// <summary>
/// A login and what it belongs to (E1.x Users screen). Staff roles and deactivation stay on the staff record (<c>PUT /api/staff/{id}</c>)
/// so roles and staff records never drift apart; <c>LinkedId</c> points there.
/// </summary>
public sealed record UserRow(
    Guid Id, string FullName, string UserName, string? Email, IReadOnlyList<string> Roles, AccountKind Kind, Guid? LinkedId, bool IsActive,
    bool IsLockedOut, bool MustChangePassword, bool HasGoogle, DateTimeOffset? LastLoginAt, DateTimeOffset CreatedAt);

/// <summary>All logins, newest first; filter by text (name, username, email), role, kind and active state (Admin).</summary>
public sealed record ListUsersQuery(string? Search = null, Role? Role = null, AccountKind? Kind = null, bool? Active = null, int? Page = null, int? PageSize = null)
    : IQuery<PagedResult<UserRow>>;

/// <summary>Disables or re-enables a parent, student or applicant login (staff: use the staff record). Disabling signs them out everywhere.</summary>
public sealed record SetUserActiveCommand(Guid Id, bool Active) : ICommand<UserRow>;

/// <summary>Issues a temporary password for any active login except your own; they must change it at next sign-in.</summary>
public sealed record ResetUserPasswordCommand(Guid Id) : ICommand<IssuedCredentials>;

public static class UserErrors
{
    public static readonly Error NotFound = Error.NotFound("user.not_found", "That account doesn't exist.");
    public static readonly Error Self = Error.Rule("user.self", "You can't do this to your own account. Ask another admin.");
    public static readonly Error StaffAccount = Error.Rule("user.staff",
        "This is a staff login. Change its roles or deactivate it from the staff member's record so their access stays consistent.");
    public static readonly Error Inactive = Error.Rule("user.inactive", "This account is disabled. Enable it first.");
}

internal static class UserQueries
{
    public static async Task<List<UserRow>> RowsAsync(this IAppDbContext db, IQueryable<AppUser> users, CancellationToken ct)
    {
        var page = await users.Select(u => new
        {
            u.Id, u.FullName, u.UserName, u.Email, u.IsActive, u.LockoutEnd, u.MustChangePassword, u.LastLoginAt, u.CreatedAt,
            Roles = db.UserRoles.Where(ur => ur.UserId == u.Id).Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name!).ToList(),
            Google = db.UserLogins.Any(l => l.UserId == u.Id && l.LoginProvider == "Google"),
            StaffId = db.Staff.Where(s => s.UserId == u.Id).Select(s => (Guid?)s.Id).FirstOrDefault(),
            GuardianId = db.Guardians.Where(g => g.UserId == u.Id).Select(g => (Guid?)g.Id).FirstOrDefault(),
            StudentId = db.Students.Where(s => s.UserId == u.Id).Select(s => (Guid?)s.Id).FirstOrDefault(),
        }).ToListAsync(ct);
        var now = DateTimeOffset.UtcNow;
        return [.. page.Select(u =>
        {
            var (kind, linked) = u.StaffId is { } st ? (AccountKind.Staff, (Guid?)st)
                : u.StudentId is { } sd ? (AccountKind.Student, (Guid?)sd)
                : u.GuardianId is { } g ? (AccountKind.Parent, (Guid?)g)
                : u.Roles.Contains(Roles.Applicant) ? (AccountKind.Applicant, null)
                : (AccountKind.Other, null);
            return new UserRow(u.Id, u.FullName, u.UserName ?? "", u.Email, u.Roles.Order().ToList(), kind, linked, u.IsActive, u.LockoutEnd > now,
                u.MustChangePassword, u.Google, u.LastLoginAt, u.CreatedAt);
        })];
    }

    public static async Task<UserRow> RowAsync(this IAppDbContext db, Guid id, CancellationToken ct) =>
        (await db.RowsAsync(db.Users.AsNoTracking().Where(u => u.Id == id), ct)).Single();
}

internal sealed class ListUsersHandler(IAppDbContext db) : IQueryHandler<ListUsersQuery, PagedResult<UserRow>>
{
    public async Task<Result<PagedResult<UserRow>>> Handle(ListUsersQuery q, CancellationToken ct)
    {
        var users = db.Users.AsNoTracking();
        if (q.Active is { } active) users = users.Where(u => u.IsActive == active);
        if (q.Role is { } role)
        {
            var name = role.ToString();
            users = users.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == name)));
        }
        users = q.Kind switch
        {
            AccountKind.Staff => users.Where(u => db.Staff.Any(s => s.UserId == u.Id)),
            AccountKind.Student => users.Where(u => db.Students.Any(s => s.UserId == u.Id)),
            AccountKind.Parent => users.Where(u => db.Guardians.Any(g => g.UserId == u.Id) && !db.Staff.Any(s => s.UserId == u.Id)),
            AccountKind.Applicant => users.Where(u => !db.Guardians.Any(g => g.UserId == u.Id) && !db.Staff.Any(s => s.UserId == u.Id)
                && db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Applicant))),
            _ => users,
        };
        foreach (var word in (q.Search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(5))
        {
            var w = word.ToLower();
            users = users.Where(u => u.FullName.ToLower().Contains(w) || u.UserName!.ToLower().Contains(w) || (u.Email != null && u.Email.ToLower().Contains(w)));
        }
        var total = await users.CountAsync(ct);
        var (page, size) = (Math.Max(1, q.Page ?? 1), Math.Clamp(q.PageSize ?? 25, 1, 100));
        var rows = await db.RowsAsync(users.OrderByDescending(u => u.CreatedAt).Skip((page - 1) * size).Take(size), ct);
        return new PagedResult<UserRow>(rows, page, size, total);
    }
}

internal sealed class SetUserActiveHandler(IAppDbContext db, UserManager<AppUser> users, ICurrentUser me, SessionService sessions)
    : ICommandHandler<SetUserActiveCommand, UserRow>
{
    public async Task<Result<UserRow>> Handle(SetUserActiveCommand c, CancellationToken ct)
    {
        if (await users.FindByIdAsync(c.Id.ToString()) is not { } user) return UserErrors.NotFound;
        if (user.Id == me.UserId) return UserErrors.Self;
        if (await db.Staff.AnyAsync(s => s.UserId == user.Id, ct)) return UserErrors.StaffAccount;
        if (user.IsActive != c.Active)
        {
            user.IsActive = c.Active;
            if (c.Active)
            {
                await users.SetLockoutEndDateAsync(user, null);
                await users.ResetAccessFailedCountAsync(user);
            }
            await users.UpdateAsync(user);
            await users.UpdateSecurityStampAsync(user);
            if (!c.Active) await sessions.RevokeAllForUserAsync(user.Id, ct);
        }
        return await db.RowAsync(user.Id, ct);
    }
}

internal sealed class ResetUserPasswordHandler(UserManager<AppUser> users, ICurrentUser me, SessionService sessions)
    : ICommandHandler<ResetUserPasswordCommand, IssuedCredentials>
{
    public async Task<Result<IssuedCredentials>> Handle(ResetUserPasswordCommand c, CancellationToken ct)
    {
        if (await users.FindByIdAsync(c.Id.ToString()) is not { } user) return UserErrors.NotFound;
        if (user.Id == me.UserId) return UserErrors.Self;
        if (!user.IsActive) return UserErrors.Inactive;
        var password = TemporaryPassword.Generate();
        if (await users.HasPasswordAsync(user)) await users.RemovePasswordAsync(user);
        var added = await users.AddPasswordAsync(user, password);
        if (!added.Succeeded) throw new InvalidOperationException("Could not set temporary password: " + string.Join("; ", added.Errors.Select(e => e.Code)));
        user.MustChangePassword = true;
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        await users.UpdateAsync(user);
        await users.UpdateSecurityStampAsync(user);
        await sessions.RevokeAllForUserAsync(user.Id, ct);
        return new IssuedCredentials(user.UserName!, password);
    }
}
