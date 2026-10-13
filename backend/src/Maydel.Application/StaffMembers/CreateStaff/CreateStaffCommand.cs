using Maydel.Application.Common.Messaging;
using Maydel.Domain.Common;

namespace Maydel.Application.StaffMembers.CreateStaff;

/// <summary>
/// Adds a staff member with a login (E4.1). The username is the email; a temporary password is generated and returned
/// once. If the email already has a non-student account (e.g. a parent), that account is linked and given the roles.
/// </summary>
public sealed record CreateStaffCommand(string FullName, string Email, string? Phone, string? JobTitle, IReadOnlyList<Role> Roles)
    : ICommand<StaffCreated>;
