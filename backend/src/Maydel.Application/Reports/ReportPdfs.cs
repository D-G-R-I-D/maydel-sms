using Maydel.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Reports;

/// <summary>Loads each student's photo and hands the cards to the PDF renderer.</summary>
public sealed class ReportPdfs(IAppDbContext db, IFileStorage storage, IReportCardRenderer renderer)
{
    public async Task<byte[]> RenderAsync(IReadOnlyList<ReportCard> cards, CancellationToken ct)
    {
        var ids = cards.Select(c => c.StudentId).ToList();
        var photos = await db.Students.AsNoTracking().Where(s => ids.Contains(s.Id) && s.PhotoPath != null)
            .ToDictionaryAsync(s => s.Id, s => s.PhotoPath!, ct);

        var documents = new List<ReportCardDocument>(cards.Count);
        foreach (var card in cards)
        {
            byte[]? photo = null;
            if (photos.TryGetValue(card.StudentId, out var key) && await storage.OpenAsync(key, ct) is { } stream)
            {
                await using (stream)
                {
                    using var buffer = new MemoryStream();
                    await stream.CopyToAsync(buffer, ct);
                    photo = buffer.ToArray();
                }
            }
            documents.Add(new ReportCardDocument(card, photo));
        }
        return renderer.Render(documents);
    }

    public static string FileName(ReportCard card) => Safe($"{card.StudentName} - {card.Term} {card.Session}") + ".pdf";

    /// <summary>File-name safe: letters, digits, spaces, dashes and dots only ("2026/2027" becomes "2026-2027").</summary>
    public static string Safe(string name) =>
        new([.. name.Replace('/', '-').Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '.' or '_')]);
}
