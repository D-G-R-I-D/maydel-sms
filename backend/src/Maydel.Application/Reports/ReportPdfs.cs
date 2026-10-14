using Maydel.Application.Abstractions;
using Maydel.Application.SchoolSettings;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Reports;

/// <summary>Loads each student's photo and the school profile (E2.6) and hands the cards to the PDF renderer.</summary>
public sealed class ReportPdfs(IAppDbContext db, IFileStorage storage, IReportCardRenderer renderer)
{
    public async Task<byte[]> RenderAsync(IReadOnlyList<ReportCard> cards, CancellationToken ct)
    {
        var ids = cards.Select(c => c.StudentId).ToList();
        var photos = await db.Students.AsNoTracking().Where(s => ids.Contains(s.Id) && s.PhotoPath != null)
            .ToDictionaryAsync(s => s.Id, s => s.PhotoPath!, ct);

        var documents = new List<ReportCardDocument>(cards.Count);
        foreach (var card in cards)
            documents.Add(new ReportCardDocument(card, await ReadAsync(photos.GetValueOrDefault(card.StudentId), ct)));

        var school = await db.SchoolProfiles.AsNoTracking().OrderBy(p => p.CreatedAt).FirstOrDefaultAsync(ct)
            ?? new Domain.Settings.SchoolProfile { Name = "School", Address = "" };
        var branding = new ReportBranding(school.Name, school.Motto, school.Address, school.Phone, school.Email, school.Website,
            school.PrincipalName, await ReadAsync(school.LogoPath, ct), await ReadAsync(school.SignaturePath, ct));
        return renderer.Render(documents, branding);
    }

    private async Task<byte[]?> ReadAsync(string? key, CancellationToken ct)
    {
        if (key is null || await storage.OpenAsync(key, ct) is not { } stream) return null;
        await using (stream)
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            return buffer.ToArray();
        }
    }

    public static string FileName(ReportCard card) => Safe($"{card.StudentName} - {card.Term} {card.Session}") + ".pdf";

    /// <summary>File-name safe: letters, digits, spaces, dashes and dots only ("2026/2027" becomes "2026-2027").</summary>
    public static string Safe(string name) =>
        new([.. name.Replace('/', '-').Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '.' or '_')]);
}
