using Maydel.Application.Abstractions;
using Maydel.Application.Photos;
using Maydel.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.SchoolSettings;

/// <summary>
/// The school's details printed on report cards (E2.6). <c>LogoVersion</c> / <c>SignatureVersion</c> are null until
/// uploaded; append them to the image URL as <c>?v=</c>.
/// </summary>
public sealed record SchoolProfileDto(
    string Name, string? Motto, string Address, string? Phone, string? Email, string? Website, string? PrincipalName,
    string? LogoVersion, string? SignatureVersion);

public enum SchoolImage
{
    Logo,
    Signature,
}

internal static class SchoolProfiles
{
    /// <summary>Logos and signatures are small on the page; 600 px is plenty for print.</summary>
    public const int MaxImageDimension = 600;

    /// <summary>The profile row (created at start-up), or a new one if somehow missing.</summary>
    public static async Task<SchoolProfile> CurrentAsync(this IAppDbContext db, CancellationToken ct)
    {
        var profile = await db.SchoolProfiles.OrderBy(p => p.CreatedAt).FirstOrDefaultAsync(ct);
        if (profile is null) db.SchoolProfiles.Add(profile = new SchoolProfile { Name = "School", Address = "" });
        return profile;
    }

    public static SchoolProfileDto ToDto(this SchoolProfile p) => new(
        p.Name, p.Motto, p.Address, p.Phone, p.Email, p.Website, p.PrincipalName,
        PhotoStore.VersionOf(p.LogoPath), PhotoStore.VersionOf(p.SignaturePath));

    public static string? PathOf(this SchoolProfile p, SchoolImage image) => image == SchoolImage.Logo ? p.LogoPath : p.SignaturePath;

    public static void SetPath(this SchoolProfile p, SchoolImage image, string? key)
    {
        if (image == SchoolImage.Logo) p.LogoPath = key;
        else p.SignaturePath = key;
    }
}
