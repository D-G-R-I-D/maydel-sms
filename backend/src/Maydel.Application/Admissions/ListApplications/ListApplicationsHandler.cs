using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.ListApplications;

internal sealed class ListApplicationsHandler(IAppDbContext db) : IQueryHandler<ListApplicationsQuery, PagedResult<ApplicationDto>>
{
    public async Task<Result<PagedResult<ApplicationDto>>> Handle(ListApplicationsQuery q, CancellationToken ct)
    {
        var apps = db.Applications.Where(a => a.SessionId == q.SessionId);
        if (q.Status is { } status) apps = apps.Where(a => a.Status == status);
        if (q.ClassLevelId is { } level) apps = apps.Where(a => a.ClassLevelId == level);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            apps = apps.Where(a => a.ReferenceNumber.ToLower().Contains(s) || (a.ChildFirstName + " " + a.ChildLastName).ToLower().Contains(s)
                || a.ParentName.ToLower().Contains(s) || a.Email.ToLower().Contains(s) || a.Phone.Contains(s));
        }
        return await db.Dtos(apps).ToPagedAsync(q.Page, q.PageSize, ct);
    }
}
