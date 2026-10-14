using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.SchoolSettings.UpdateSchoolProfile;

internal sealed class UpdateSchoolProfileHandler(IAppDbContext db) : ICommandHandler<UpdateSchoolProfileCommand, SchoolProfileDto>
{
    public async Task<Result<SchoolProfileDto>> Handle(UpdateSchoolProfileCommand c, CancellationToken ct)
    {
        var profile = await db.CurrentAsync(ct);
        (profile.Name, profile.Motto, profile.Address) = (c.Name.Trim(), Clean(c.Motto), c.Address.Trim());
        (profile.Phone, profile.Email, profile.Website, profile.PrincipalName) = (Clean(c.Phone), Clean(c.Email), Clean(c.Website), Clean(c.PrincipalName));
        await db.SaveChangesAsync(ct);
        return profile.ToDto();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
