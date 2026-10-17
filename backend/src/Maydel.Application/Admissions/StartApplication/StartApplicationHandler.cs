using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Identity;
using Maydel.Domain.Admissions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.StartApplication;

internal sealed class StartApplicationHandler(IAppDbContext db, UserManager<AppUser> users, ICurrentUser me) : ICommandHandler<StartApplicationCommand, ApplicationDto>
{
    public async Task<Result<ApplicationDto>> Handle(StartApplicationCommand c, CancellationToken ct)
    {
        var child = c.Child;
        if (string.IsNullOrWhiteSpace(child.ChildFirstName) || string.IsNullOrWhiteSpace(child.ChildLastName))
            return Error.Validation("childFirstName", "Enter the child's first and last name.");
        if (await db.CheckOpenAsync(child.SessionId, child.ClassLevelId, ct) is { } closed) return closed;
        var user = await users.FindByIdAsync(me.UserId!.Value.ToString());
        if (user is null) return AdmissionErrors.ApplicationNotFound;
        var phone = await db.Guardians.Where(g => g.UserId == user.Id).Select(g => g.Phone).FirstOrDefaultAsync(ct)
            ?? await db.Applications.Where(a => a.ApplicantUserId == user.Id).OrderByDescending(a => a.CreatedAt).Select(a => a.Phone).FirstOrDefaultAsync(ct)
            ?? user.PhoneNumber ?? "";

        var fee = await db.AdmissionSettings.Where(s => s.SessionId == child.SessionId).Select(s => s.FormFee).FirstAsync(ct);
        var application = new AdmissionApplication
        {
            ReferenceNumber = await db.NextReferenceAsync(child.SessionId, ct), SessionId = child.SessionId, ClassLevelId = child.ClassLevelId, ApplicantUserId = user.Id,
            ParentName = user.FullName, Phone = phone, Email = user.Email ?? user.UserName!, ChildFirstName = child.ChildFirstName.Trim(),
            ChildLastName = child.ChildLastName.Trim(), ChildDateOfBirth = child.ChildDateOfBirth, FormFee = fee,
        };
        db.Applications.Add(application);
        await db.SaveChangesAsync(ct);
        return await db.Dtos(db.Applications.Where(a => a.Id == application.Id)).FirstAsync(ct);
    }
}
