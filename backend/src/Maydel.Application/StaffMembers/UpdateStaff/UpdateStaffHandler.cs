using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Identity;
using Maydel.Domain.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.StaffMembers.UpdateStaff;

internal sealed class UpdateStaffHandler(IAppDbContext db, UserManager<AppUser> users, ICurrentUser me, SessionService sessions)
    : ICommandHandler<UpdateStaffCommand, StaffDetail>
{
    public async Task<Result<StaffDetail>> Handle(UpdateStaffCommand c, CancellationToken ct)
    {
        var staff = await db.Staff.FirstOrDefaultAsync(s => s.Id == c.Id, ct);
        if (staff is null) return StaffErrors.NotFound;

        var wantedRoles = c.Roles.Distinct().ToHashSet();
        if (staff.UserId is { } self && self == me.UserId && (!c.IsActive || !wantedRoles.Contains(Role.Admin))) return StaffErrors.SelfLockout;

        var email = c.Email.Trim().ToLowerInvariant();
        if (await db.Staff.AnyAsync(s => s.Id != staff.Id && s.Email == email, ct)) return StaffErrors.DuplicateEmail;
        var normalized = users.NormalizeEmail(email);
        if (await users.Users.AnyAsync(u => u.Id != staff.UserId && (u.NormalizedEmail == normalized || u.NormalizedUserName == normalized), ct))
            return StaffErrors.EmailInUse;

        staff.FullName = c.FullName.Trim();
        staff.Phone = string.IsNullOrWhiteSpace(c.Phone) ? null : c.Phone.Trim();
        staff.JobTitle = string.IsNullOrWhiteSpace(c.JobTitle) ? null : c.JobTitle.Trim();
        var emailChanged = staff.Email != email;
        staff.Email = email;
        staff.IsActive = c.IsActive;

        if (staff.UserId is { } userId && await users.FindByIdAsync(userId.ToString()) is { } user)
        {
            var sessionChanged = false;
            user.FullName = staff.FullName;
            if (emailChanged)
            {
                await users.SetEmailAsync(user, email);
                await users.SetUserNameAsync(user, email); // staff sign in with their email
                user.EmailConfirmed = true;
                sessionChanged = true;
            }

            // Only staff roles are managed here; a Parent role on the same account is left alone.
            var current = (await users.GetRolesAsync(user)).Select(Enum.Parse<Role>).Where(StaffRoles.IsStaffRole).ToHashSet();
            var target = c.IsActive ? wantedRoles : [];
            var remove = current.Except(target).Select(r => r.ToString()).ToList();
            var add = target.Except(current).Select(r => r.ToString()).ToList();
            if (remove.Count > 0) await users.RemoveFromRolesAsync(user, remove);
            if (add.Count > 0) await users.AddToRolesAsync(user, add);
            sessionChanged |= remove.Count > 0 || add.Count > 0;

            // The account stays usable if it still has other roles (e.g. a parent who has left the staff).
            var stillUsable = c.IsActive || (await users.GetRolesAsync(user)).Count > 0;
            if (user.IsActive != stillUsable)
            {
                user.IsActive = stillUsable;
                sessionChanged = true;
            }

            await users.UpdateAsync(user);
            if (sessionChanged)
            {
                await users.UpdateSecurityStampAsync(user);
                await sessions.RevokeAllForUserAsync(user.Id, ct);
            }
        }

        await db.SaveChangesAsync(ct);
        return (await db.GetStaffDetailAsync(staff.Id, ct))!;
    }
}
