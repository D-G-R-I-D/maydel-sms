using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Identity;
using Maydel.Domain.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Students.IssueStudentLogin;

internal sealed class IssueStudentLoginHandler(IAppDbContext db, UserManager<AppUser> users, SessionService sessions)
    : ICommandHandler<IssueStudentLoginCommand, IssuedCredentials>
{
    public async Task<Result<IssuedCredentials>> Handle(IssueStudentLoginCommand c, CancellationToken ct)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == c.Id, ct);
        if (student is null) return StudentErrors.NotFound;
        if (!student.IsActive) return StudentErrors.InactiveStudent;

        if (student.UserId is { } userId && await users.FindByIdAsync(userId.ToString()) is { } existing)
            return await users.ResetToTemporaryPasswordAsync(existing, sessions, ct);

        var password = TemporaryPassword.Generate();
        var user = new AppUser { UserName = student.AdmissionNumber, FullName = student.FullName, MustChangePassword = true };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded) throw new InvalidOperationException("Could not create student login: " + string.Join("; ", created.Errors.Select(e => e.Code)));
        await users.AddToRoleAsync(user, Roles.Student);

        student.UserId = user.Id;
        await db.SaveChangesAsync(ct);
        return new IssuedCredentials(user.UserName, password);
    }
}
