using Maydel.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance;

/// <summary>Next number in a per-session sequence. Unique indexes on the numbers guard against concurrent writers.</summary>
internal static class Numbers
{
    public static async Task<int> NextReceiptAsync(this IAppDbContext db, string prefix, CancellationToken ct)
    {
        var used = await db.Payments.Where(p => p.ReceiptNumber.StartsWith(prefix)).Select(p => p.ReceiptNumber).ToListAsync(ct);
        var local = db.Payments.Local.Where(p => p.ReceiptNumber.StartsWith(prefix)).Select(p => p.ReceiptNumber);
        var all = used.Concat(local).ToList();
        return all.Count == 0 ? 1 : all.Max(n => int.Parse(n[prefix.Length..])) + 1;
    }
}
