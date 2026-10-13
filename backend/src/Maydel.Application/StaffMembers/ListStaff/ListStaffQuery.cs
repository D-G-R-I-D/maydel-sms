using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.StaffMembers.ListStaff;

/// <summary>Staff directory for admins. <c>search</c> matches name, email or staff number; <c>isActive</c> filters.</summary>
public sealed record ListStaffQuery(string? Search = null, bool? IsActive = null, int? Page = null, int? PageSize = null)
    : IQuery<PagedResult<StaffListItem>>;
