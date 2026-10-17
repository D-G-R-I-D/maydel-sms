using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Admissions;

namespace Maydel.Application.Admissions.ListApplications;

/// <summary>Applications for a session, newest first; filter by status or class level, or search by reference, child or parent.</summary>
public sealed record ListApplicationsQuery(
    Guid SessionId, ApplicationStatus? Status = null, Guid? ClassLevelId = null, string? Search = null, int? Page = null, int? PageSize = null)
    : IQuery<PagedResult<ApplicationDto>>;
