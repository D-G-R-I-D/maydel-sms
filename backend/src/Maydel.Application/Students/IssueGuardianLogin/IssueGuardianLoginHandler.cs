using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Identity;
using Maydel.Domain.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Students.IssueGuardianLogin;

internal sealed class IssueGuardianLoginHandler(IAppDbContext db, UserManager<AppUser> users, SessionService sessions)
    : ICommandHandler<IssueGuardianLoginCommand, GuardianLoginIssued>
{
    public async Task<Result<GuardianLoginIssued>> Handle(IssueGuardianLoginCommand c, CancellationToken ct)
    {
        var guardian = await db.Guardians.FirstOrDefaultAsync(g => g.Id == c.Id, ct);
        if (guardian is null) return StudentErrors.GuardianNotFound;

        if (guardian.UserId is { } userId && await users.FindByIdAsync(userId.ToString()) is { } current)
            return new GuardianLoginIssued(await users.ResetToTemporaryPasswordAsync(current, sessions, ct), false);

        var userName = guardian.Email ?? guardian.Phone;
        var existing = await users.Users
            .Where(u => u.NormalizedUserName == users.NormalizeName(userName) || (guardian.Email != null && u.NormalizedEmail == users.NormalizeEmail(guardian.Email)))
            .Take(2).ToListAsync(ct);
        if (existing.Count > 1) return StudentErrors.LoginInUse;

        if (existing is [var account])
        {
            if (await users.IsInRoleAsync(account, Roles.Student) || await db.Guardians.AnyAsync(g => g.UserId == account.Id, ct))
                return StudentErrors.LoginInUse;
            if (!await users.IsInRoleAsync(account, Roles.Parent))
            {
                await users.AddToRoleAsync(account, Roles.Parent);
                await users.UpdateSecurityStampAsync(account);
            }
            guardian.UserId = account.Id;
            await db.SaveChangesAsync(ct);
            return new GuardianLoginIssued(null, true);
        }

        var password = TemporaryPassword.Generate();
        var user = new AppUser
        {
            UserName = userName, Email = guardian.Email, EmailConfirmed = guardian.Email is not null,
            FullName = guardian.FullName, MustChangePassword = true,
        };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded) throw new InvalidOperationException("Could not create parent login: " + string.Join("; ", created.Errors.Select(e => e.Code)));
        await users.AddToRoleAsync(user, Roles.Parent);

        guardian.UserId = user.Id;
        await db.SaveChangesAsync(ct);
        return new GuardianLoginIssued(new IssuedCredentials(userName, password), false);
    }
}
