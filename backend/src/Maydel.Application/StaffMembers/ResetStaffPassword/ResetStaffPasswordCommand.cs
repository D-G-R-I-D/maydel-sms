using Maydel.Application.Common.Messaging;
using Maydel.Application.Identity;

namespace Maydel.Application.StaffMembers.ResetStaffPassword;

/// <summary>
/// Issues a new temporary password (shown once), unlocks the account, forces a change at next sign-in and signs the
/// user out everywhere (E1.5 staff reset via admin). Not for your own account: use Change password.
/// </summary>
public sealed record ResetStaffPasswordCommand(Guid Id) : ICommand<IssuedCredentials>;
