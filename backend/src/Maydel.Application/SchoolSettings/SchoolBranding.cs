using Maydel.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.SchoolSettings;

/// <summary>The school profile plus its logo and signature bytes, for every printed document (E2.6).</summary>
public sealed class SchoolBranding(IAppDbContext db, IFileStorage storage)
{
    public async Task<ReportBranding> LoadAsync(CancellationToken ct)
    {
        var school = await db.SchoolProfiles.AsNoTracking().OrderBy(p => p.CreatedAt).FirstOrDefaultAsync(ct)
            ?? new Domain.Settings.SchoolProfile { Name = "School", Address = "" };
        return new ReportBranding(school.Name, school.Motto, school.Address, school.Phone, school.Email, school.Website,
            school.PrincipalName, await ReadAsync(school.LogoPath, ct), await ReadAsync(school.SignaturePath, ct));
    }

    public async Task<byte[]?> ReadAsync(string? key, CancellationToken ct)
    {
        if (key is null || await storage.OpenAsync(key, ct) is not { } stream) return null;
        await using (stream)
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            return buffer.ToArray();
        }
    }
}
