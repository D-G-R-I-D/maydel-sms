using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Identity;
using Maydel.Domain.People;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Students.UpdateGuardian;

internal sealed class UpdateGuardianHandler(IAppDbContext db, UserManager<AppUser> users) : ICommandHandler<UpdateGuardianCommand, GuardianDetail>
{
    public async Task<Result<GuardianDetail>> Handle(UpdateGuardianCommand c, CancellationToken ct)
    {
        var guardian = await db.Guardians.FirstOrDefaultAsync(g => g.Id == c.Id, ct);
        if (guardian is null) return StudentErrors.GuardianNotFound;

        var phone = Guardian.NormalizePhone(c.Phone)!;
        if (await db.Guardians.AnyAsync(g => g.Id != guardian.Id && g.Phone == phone, ct)) return StudentErrors.PhoneInUse;
        var email = string.IsNullOrWhiteSpace(c.Email) ? null : c.Email.Trim().ToLowerInvariant();

        // A parent login's username follows its email or phone, so keep them in step.
        if (guardian.UserId is { } userId && await users.FindByIdAsync(userId.ToString()) is { } user)
        {
            var newUserName = email ?? phone;
            if (!string.Equals(user.UserName, newUserName, StringComparison.OrdinalIgnoreCase)
                && await users.Users.AnyAsync(u => u.Id != user.Id && (u.NormalizedUserName == users.NormalizeName(newUserName)
                    || (email != null && u.NormalizedEmail == users.NormalizeEmail(email))), ct))
                return StudentErrors.LoginInUse;
            if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)) await users.SetEmailAsync(user, email);
            if (!string.Equals(user.UserName, newUserName, StringComparison.OrdinalIgnoreCase)) await users.SetUserNameAsync(user, newUserName);
            user.EmailConfirmed = email is not null;
            user.FullName = c.FullName.Trim();
            await users.UpdateAsync(user);
        }

        guardian.FullName = c.FullName.Trim();
        guardian.Phone = phone;
        guardian.Email = email;
        guardian.Occupation = string.IsNullOrWhiteSpace(c.Occupation) ? null : c.Occupation.Trim();
        guardian.Address = string.IsNullOrWhiteSpace(c.Address) ? null : c.Address.Trim();
        await db.SaveChangesAsync(ct);
        return (await db.GetGuardianDetailAsync(guardian.Id, ct))!;
    }
}
