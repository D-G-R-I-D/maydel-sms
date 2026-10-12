using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.AspNetCore.Identity;

namespace Maydel.Application.Identity.ChangePassword;

internal sealed class ChangePasswordHandler(ICurrentUser me, UserManager<AppUser> users, SessionService sessions)
    : ICommandHandler<ChangePasswordCommand, AuthResponse>
{
    public async Task<Result<AuthResponse>> Handle(ChangePasswordCommand command, CancellationToken ct)
    {
        if (me.UserId is not { } id || await users.FindByIdAsync(id.ToString()) is not { } user) return IdentityErrors.SessionExpired;

        if (!await users.CheckPasswordAsync(user, command.CurrentPassword))
            return Error.Validation("currentPassword", "Your current password is incorrect.");

        // Also rotates the security stamp, which invalidates every access token issued before now.
        var changed = await users.ChangePasswordAsync(user, command.CurrentPassword, command.NewPassword);
        if (!changed.Succeeded)
            return new ValidationError(new Dictionary<string, string[]> { ["newPassword"] = [.. changed.Errors.Select(e => e.Description)] });

        user.MustChangePassword = false;
        await users.UpdateAsync(user);
        await sessions.RevokeAllForUserAsync(user.Id, ct);
        return await sessions.IssueAsync(user, ct: ct);
    }
}
