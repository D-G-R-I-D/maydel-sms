using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Website.StaffProfiles;

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
