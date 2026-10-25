using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Website.StaffProfiles;

/// <summary>A staff member as the website console edits them: shown or not, job title, bio, order, and whether there's a photo.</summary>
public sealed record WebsiteStaffRow(Guid StaffId, string Name, string? JobTitle, bool ShowOnWebsite, string? Bio, int WebsiteOrder, bool HasPhoto, string? PhotoVersion);

/// <summary>Every active staff member, those on the website first (in website order), then the rest by name.</summary>
public sealed record ListWebsiteStaffQuery : IQuery<IReadOnlyList<WebsiteStaffRow>>;

internal sealed class ListWebsiteStaffHandler(IAppDbContext db) : IQueryHandler<ListWebsiteStaffQuery, IReadOnlyList<WebsiteStaffRow>>
{
    public async Task<Result<IReadOnlyList<WebsiteStaffRow>>> Handle(ListWebsiteStaffQuery q, CancellationToken ct) =>
        (await db.Staff.AsNoTracking().Where(s => s.IsActive)
            .OrderByDescending(s => s.ShowOnWebsite).ThenBy(s => s.WebsiteOrder).ThenBy(s => s.FullName).ToListAsync(ct))
        .Select(s => new WebsiteStaffRow(s.Id, s.FullName, s.JobTitle, s.ShowOnWebsite, s.Bio, s.WebsiteOrder, s.PhotoPath is not null,
            Photos.PhotoStore.VersionOf(s.PhotoPath))).ToList();
}

/// <summary>Shows a staff member on the website's Staff page with their job title, short bio and order (E7.3). Photo: the staff photo.</summary>
public sealed record SetStaffWebsiteCommand(Guid StaffId, StaffWebsiteInput Profile) : ICommand<PublicStaff>;

internal sealed class SetStaffWebsiteHandler(IAppDbContext db) : ICommandHandler<SetStaffWebsiteCommand, PublicStaff>
{
    public async Task<Result<PublicStaff>> Handle(SetStaffWebsiteCommand c, CancellationToken ct)
    {
        if (await db.Staff.FirstOrDefaultAsync(s => s.Id == c.StaffId, ct) is not { } staff) return Error.NotFound("staff.not_found", "That staff member doesn't exist.");
        var p = c.Profile;
        if (p.JobTitle is { Length: > 100 }) return Error.Validation("jobTitle", "Keep the job title under 100 characters.");
        if (p.Bio is { Length: > 2000 }) return Error.Validation("bio", "Keep the bio under 2000 characters.");
        (staff.ShowOnWebsite, staff.JobTitle, staff.Bio, staff.WebsiteOrder) =
            (p.ShowOnWebsite, string.IsNullOrWhiteSpace(p.JobTitle) ? null : p.JobTitle.Trim(), string.IsNullOrWhiteSpace(p.Bio) ? null : p.Bio.Trim(), p.WebsiteOrder);
        await db.SaveChangesAsync(ct);
        return new PublicStaff(staff.Id, staff.FullName, staff.JobTitle, staff.Bio, WebsiteUrls.Image("staff", staff.Id, staff.PhotoPath));
    }
}
