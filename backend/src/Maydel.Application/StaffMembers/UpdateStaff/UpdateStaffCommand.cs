using Maydel.Application.Common.Messaging;
using Maydel.Domain.Common;

namespace Maydel.Application.StaffMembers.UpdateStaff;

/// <summary>
/// Edits a staff member, their roles and active status. Role changes and deactivation take effect on the user's next
/// request (security stamp) and sign them out everywhere. Admins can't remove their own Admin role or deactivate themselves.
/// </summary>
public sealed record UpdateStaffCommand(Guid Id, string FullName, string Email, string? Phone, string? JobTitle, IReadOnlyList<Role> Roles, bool IsActive)
    : ICommand<StaffDetail>;
