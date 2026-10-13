using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Identity;
using Maydel.Domain.Common;
using Maydel.Domain.People;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.StaffMembers.CreateStaff;

internal sealed class CreateStaffHandler(IAppDbContext db, UserManager<AppUser> users) : ICommandHandler<CreateStaffCommand, StaffCreated>
{
    public async Task<Result<StaffCreated>> Handle(CreateStaffCommand c, CancellationToken ct)
    {
        var email = c.Email.Trim().ToLowerInvariant();
        var fullName = c.FullName.Trim();
        if (await db.Staff.AnyAsync(s => s.Email == email, ct)) return StaffErrors.DuplicateEmail;

        var normalized = users.NormalizeEmail(email);
        var existing = await users.Users.Where(u => u.NormalizedEmail == normalized || u.NormalizedUserName == users.NormalizeName(email)).Take(2).ToListAsync(ct);
        if (existing.Count > 1) return StaffErrors.EmailInUse;

        AppUser user;
        IssuedCredentials? credentials = null;
        if (existing is [var account])
        {
            if (await users.IsInRoleAsync(account, Roles.Student)) return StaffErrors.StudentAccount;
            if (await db.Staff.AnyAsync(s => s.UserId == account.Id, ct)) return StaffErrors.EmailInUse;
            user = account;
            user.IsActive = true;
            await users.UpdateAsync(user);
        }
        else
        {
            var password = TemporaryPassword.Generate();
            user = new AppUser { UserName = email, Email = email, EmailConfirmed = true, FullName = fullName, MustChangePassword = true };
            var created = await users.CreateAsync(user, password);
            if (!created.Succeeded) throw new InvalidOperationException("Could not create staff login: " + string.Join("; ", created.Errors.Select(e => e.Code)));
            credentials = new IssuedCredentials(email, password);
        }

        var missing = c.Roles.Distinct().Select(r => r.ToString()).Except(await users.GetRolesAsync(user)).ToList();
        if (missing.Count > 0)
        {
            await users.AddToRolesAsync(user, missing);
            await users.UpdateSecurityStampAsync(user); // existing sessions pick up the new roles
        }

        var staff = new Staff
        {
            StaffNumber = await db.NextStaffNumberAsync(ct),
            FullName = fullName,
            Email = email,
            Phone = string.IsNullOrWhiteSpace(c.Phone) ? null : c.Phone.Trim(),
            JobTitle = string.IsNullOrWhiteSpace(c.JobTitle) ? null : c.JobTitle.Trim(),
            UserId = user.Id,
        };
        db.Staff.Add(staff);
        await db.SaveChangesAsync(ct);

        return new StaffCreated((await db.GetStaffDetailAsync(staff.Id, ct))!, credentials, credentials is null);
    }
}
