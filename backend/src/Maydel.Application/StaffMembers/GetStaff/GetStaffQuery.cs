using Maydel.Application.Common.Messaging;

namespace Maydel.Application.StaffMembers.GetStaff;

/// <summary>One staff member with roles, login status and their class / subject assignments.</summary>
public sealed record GetStaffQuery(Guid Id) : IQuery<StaffDetail>;
