using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Identity;
using Maydel.Application.Identity.GoogleLogin;
using Maydel.Domain.Admissions;
using Maydel.Domain.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.RegisterWithGoogle;

internal sealed class RegisterWithGoogleHandler(IAppDbContext db, UserManager<AppUser> users, IGoogleIdTokenVerifier google, SessionService sessions, TimeProvider clock)
    : ICommandHandler<RegisterWithGoogleCommand, GoogleRegistration>
{
    public async Task<Result<GoogleRegistration>> Handle(RegisterWithGoogleCommand c, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(c.Website)) return Error.Validation("idToken", "Please try again."); // honeypot: bots only
        if (!google.IsEnabled) return IdentityErrors.GoogleDisabled;
        if (await db.CheckOpenAsync(c.SessionId, c.ClassLevelId, ct) is { } closed) return closed;
        if (await google.VerifyAsync(c.IdToken, ct) is not { } identity) return IdentityErrors.GoogleInvalid;
        if (!identity.EmailVerified) return IdentityErrors.GoogleEmailUnverified;

        var email = identity.Email.Trim().ToLowerInvariant();
        if (await users.FindByEmailAsync(email) is not null || await users.FindByNameAsync(email) is not null
            || await users.FindByLoginAsync(GoogleLoginHandler.Provider, identity.Subject) is not null)
            return AdmissionErrors.EmailInUse;

        // No password: this account signs in with Google (a password can be set later with "forgot password").
        var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true, FullName = c.ParentName.Trim() };
        var created = await users.CreateAsync(user);
        if (!created.Succeeded) throw new InvalidOperationException("Could not create applicant: " + string.Join("; ", created.Errors.Select(e => e.Code)));
        await users.AddToRoleAsync(user, Roles.Applicant);
        var linked = await users.AddLoginAsync(user, new UserLoginInfo(GoogleLoginHandler.Provider, identity.Subject, GoogleLoginHandler.Provider));
        if (!linked.Succeeded) throw new InvalidOperationException("Could not link Google login: " + linked);

        var fee = await db.AdmissionSettings.Where(s => s.SessionId == c.SessionId).Select(s => s.FormFee).FirstAsync(ct);
        var application = new AdmissionApplication
        {
            ReferenceNumber = await db.NextReferenceAsync(c.SessionId, ct), SessionId = c.SessionId, ClassLevelId = c.ClassLevelId, ApplicantUserId = user.Id,
            ParentName = c.ParentName.Trim(), Phone = c.Phone.Trim(), Email = email, ChildFirstName = c.ChildFirstName.Trim(), ChildLastName = c.ChildLastName.Trim(),
            ChildDateOfBirth = c.ChildDateOfBirth, FormFee = fee,
        };
        db.Applications.Add(application);
        await db.SaveChangesAsync(ct);
        await db.RecordSuccessfulSignInAsync(user.Id, clock, ct);
        var session = await sessions.IssueAsync(user, ct: ct);
        return new GoogleRegistration(session, await db.Dtos(db.Applications.Where(a => a.Id == application.Id)).FirstAsync(ct));
    }
}
