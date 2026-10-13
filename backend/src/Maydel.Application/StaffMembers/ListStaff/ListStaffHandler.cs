using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.StaffMembers.ListStaff;

internal sealed class ListStaffHandler(IAppDbContext db) : IQueryHandler<ListStaffQuery, PagedResult<StaffListItem>>
{
    public async Task<Result<PagedResult<StaffListItem>>> Handle(ListStaffQuery q, CancellationToken ct)
    {
        var staff = db.Staff.AsNoTracking();
        if (q.IsActive is { } active) staff = staff.Where(s => s.IsActive == active);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim().ToLower();
            staff = staff.Where(s => s.FullName.ToLower().Contains(term) || s.Email.ToLower().Contains(term) || s.StaffNumber.ToLower().Contains(term));
        }

        var page = await staff.OrderBy(s => s.FullName)
            .Select(s => new
            {
                s.Id, s.StaffNumber, s.FullName, s.Email, s.Phone, s.JobTitle, s.IsActive, s.UserId, s.PhotoPath,
                LastLoginAt = db.Users.Where(u => u.Id == s.UserId).Select(u => u.LastLoginAt).FirstOrDefault(),
            })
            .ToPagedAsync(q.Page, q.PageSize, ct);

        var roles = await db.RolesByUserAsync([.. page.Items.Where(s => s.UserId is not null).Select(s => s.UserId!.Value)], ct);
        return new PagedResult<StaffListItem>(
            [.. page.Items.Select(s => new StaffListItem(
                s.Id, s.StaffNumber, s.FullName, s.Email, s.Phone, s.JobTitle,
                s.UserId is { } uid ? roles.GetValueOrDefault(uid, []) : [], s.IsActive, s.UserId is not null, s.LastLoginAt,
                Photos.PhotoStore.VersionOf(s.PhotoPath)))],
            page.Page, page.PageSize, page.TotalCount);
    }
}
