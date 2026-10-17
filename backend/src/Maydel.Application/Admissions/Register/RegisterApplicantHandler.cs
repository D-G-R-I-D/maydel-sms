using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Identity;
using Maydel.Domain.Admissions;
using Maydel.Domain.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.Register;

internal sealed class RegisterApplicantHandler(IAppDbContext db, UserManager<AppUser> users) : ICommandHandler<RegisterApplicantCommand, ApplicationDto>
{
    public async Task<Result<ApplicationDto>> Handle(RegisterApplicantCommand c, CancellationToken ct)
    {
        if (await db.CheckOpenAsync(c.SessionId, c.ClassLevelId, ct) is { } closed) return closed;
        var email = c.Email.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(c.Website)) return Error.Validation("email", "Please try again."); // honeypot: bots only
        if (await users.FindByEmailAsync(email) is not null || await users.FindByNameAsync(email) is not null) return AdmissionErrors.EmailInUse;

        var user = new AppUser { UserName = email, Email = email, EmailConfirmed = false, FullName = c.ParentName.Trim() };
        var created = await users.CreateAsync(user, c.Password);
        if (!created.Succeeded)
            return Error.Validation("password", string.Join(" ", created.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(user, Roles.Applicant);

        var fee = await db.AdmissionSettings.Where(s => s.SessionId == c.SessionId).Select(s => s.FormFee).FirstAsync(ct);
        var application = new AdmissionApplication
        {
            ReferenceNumber = await db.NextReferenceAsync(c.SessionId, ct), SessionId = c.SessionId, ClassLevelId = c.ClassLevelId, ApplicantUserId = user.Id,
            ParentName = c.ParentName.Trim(), Phone = c.Phone.Trim(), Email = email, ChildFirstName = c.ChildFirstName.Trim(), ChildLastName = c.ChildLastName.Trim(),
            ChildDateOfBirth = c.ChildDateOfBirth, FormFee = fee,
        };
        db.Applications.Add(application);
        await db.SaveChangesAsync(ct);
        return await db.Dtos(db.Applications.Where(a => a.Id == application.Id)).FirstAsync(ct);
    }
}
