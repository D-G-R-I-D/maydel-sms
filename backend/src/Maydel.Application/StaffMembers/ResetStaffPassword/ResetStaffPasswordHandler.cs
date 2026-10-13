using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.StaffMembers.ResetStaffPassword;

internal sealed class ResetStaffPasswordHandler(IAppDbContext db, UserManager<AppUser> users, ICurrentUser me, SessionService sessions)
    : ICommandHandler<ResetStaffPasswordCommand, IssuedCredentials>
{
    public async Task<Result<IssuedCredentials>> Handle(ResetStaffPasswordCommand c, CancellationToken ct)
    {
        var staff = await db.Staff.AsNoTracking().FirstOrDefaultAsync(s => s.Id == c.Id, ct);
        if (staff is null) return StaffErrors.NotFound;
        if (staff.UserId is not { } userId || await users.FindByIdAsync(userId.ToString()) is not { } user) return StaffErrors.NoLogin;
        if (userId == me.UserId) return StaffErrors.SelfReset;
        if (!staff.IsActive || !user.IsActive) return StaffErrors.Inactive;

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
