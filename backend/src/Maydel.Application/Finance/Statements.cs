using Maydel.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance;

internal static class Statements
{
    /// <summary>Cancelled invoices are left out entirely (they never had money on them).</summary>
    public static async Task<StudentStatement?> ForAsync(IAppDbContext db, Guid studentId, CancellationToken ct)
    {
        var student = await db.Students.AsNoTracking().Where(s => s.Id == studentId)
            .Select(s => new { s.AdmissionNumber, Name = s.FirstName + " " + s.LastName,
                Room = s.ClassRoom != null ? s.ClassRoom.ClassLevel.Name + " " + s.ClassRoom.Arm : null })
            .FirstOrDefaultAsync(ct);
        if (student is null) return null;

        var invoices = await db.Invoices.AsNoTracking().Include(i => i.Payments).Include(i => i.Term).ThenInclude(t => t.Session)
            .Where(i => i.StudentId == studentId && !i.IsCancelled).ToListAsync(ct);
        var raw = new List<(DateOnly Date, DateTimeOffset At, string Kind, string Reference, string Description, decimal Debit, decimal Credit)>();
        foreach (var i in invoices)
        {
            raw.Add((DateOnly.FromDateTime(i.CreatedAt.UtcDateTime), i.CreatedAt, "Invoice", i.Number, $"{i.Term.Number} Term {i.Term.Session.Name} fees", i.Total, 0));
            foreach (var p in i.Payments)
                raw.Add(p.IsReversal
                    ? (p.PaidOn, p.CreatedAt, "Reversal", p.ReceiptNumber, $"Reversal of {p.Reference}: {p.Note}", -p.Amount, 0)
                    : (p.PaidOn, p.CreatedAt, "Payment", p.ReceiptNumber, $"{p.Method} payment{(p.Reference is null ? "" : $" ({p.Reference})")}", 0, p.Amount));
        }
        decimal running = 0;
        // In the order they were recorded, so a back-dated payment never shows before the invoice it pays.
        var entries = raw.OrderBy(e => e.At)
            .Select(e => { running += e.Debit - e.Credit; return new StatementEntry(e.Date, e.Kind, e.Reference, e.Description, e.Debit, e.Credit, running); })
            .ToList();
        var billed = invoices.Sum(i => i.Total);
        var paid = invoices.Sum(i => i.AmountPaid);
        return new StudentStatement(studentId, student.AdmissionNumber, student.Name, student.Room, billed, paid, billed - paid, entries);
    }
}
