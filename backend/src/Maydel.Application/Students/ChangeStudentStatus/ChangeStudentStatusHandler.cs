using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Students.ChangeStudentStatus;

internal sealed class ChangeStudentStatusHandler(IAppDbContext db, UserManager<AppUser> users, SessionService sessions)
    : ICommandHandler<ChangeStudentStatusCommand, StudentDetail>
{
    public async Task<Result<StudentDetail>> Handle(ChangeStudentStatusCommand c, CancellationToken ct)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == c.Id, ct);
        if (student is null) return StudentErrors.NotFound;

        if (c.Status == Domain.People.StudentStatus.Active && student.ClassRoomId is { } classRoomId
            && await db.CheckClassHasRoomAsync(classRoomId, student.Id, ct) is { } full) return full;
        if (student.ChangeStatus(c.Status) is { } invalid) return invalid.ToError();

        if (student.UserId is { } userId && await users.FindByIdAsync(userId.ToString()) is { } user && user.IsActive != student.IsActive)
        {
            user.IsActive = student.IsActive;
            await users.UpdateAsync(user);
            await users.UpdateSecurityStampAsync(user);
            if (!student.IsActive) await sessions.RevokeAllForUserAsync(user.Id, ct);
        }

        await db.SaveChangesAsync(ct);
        return (await db.GetStudentDetailAsync(student.Id, ct))!;
    }
}
