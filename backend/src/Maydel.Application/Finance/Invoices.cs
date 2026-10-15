using Maydel.Application.Abstractions;
using Maydel.Domain.Academics;
using Maydel.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance;

internal static class Invoices
{
    public static InvoiceStatus StatusOf(bool cancelled, decimal total, decimal paid) =>
        cancelled ? InvoiceStatus.Cancelled : paid <= 0 ? InvoiceStatus.Unpaid : paid < total ? InvoiceStatus.PartiallyPaid : InvoiceStatus.Paid;

    /// <summary>The instalment schedule for an invoice, or empty when its term has no enabled plan (pay in full).</summary>
    public static async Task<IReadOnlyList<ScheduleItem>> ScheduleAsync(this IAppDbContext db, Term term, decimal total, CancellationToken ct)
    {
        if (await db.PlanForAsync(term, ct) is not { Plan.Enabled: true } p) return [];
        var parts = p.Plan.Instalments.OrderBy(x => x.Order).ToList();
        var amounts = FeeRules.Split(total, [.. parts.Select(x => x.Percent)]);
        return [.. parts.Select((x, n) => new ScheduleItem(x.Order, x.Percent, amounts[n], x.DueOn))];
    }

    public static async Task<InvoiceDetail?> DetailAsync(this IAppDbContext db, Guid id, DateOnly today, CancellationToken ct)
    {
        var i = await db.Invoices.AsNoTracking().Include(x => x.Payments).Include(x => x.Student).Include(x => x.Term).ThenInclude(t => t.Session)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (i is null) return null;
        var room = await db.ClassRooms.AsNoTracking().Where(r => r.Id == i.ClassRoomId).Select(r => r.ClassLevel.Name + " " + r.Arm).FirstOrDefaultAsync(ct);
        var schedule = i.IsCancelled ? [] : await db.ScheduleAsync(i.Term, i.Total, ct);
        var dueNow = i.IsCancelled ? 0 : FeeRules.DueBy(today, i.Total, i.DueOn, [.. schedule.Select(s => (s.Amount, s.DueOn))]);
        var receipts = i.Payments.ToDictionary(p => p.Id, p => p.ReceiptNumber);
        var payments = i.Payments.OrderBy(p => p.CreatedAt).Select(p => new PaymentDto(
            p.Id, p.ReceiptNumber, p.Amount, p.Method, p.Reference, p.PaidOn, p.IsReversal,
            p.ReversesPaymentId is { } r ? receipts.GetValueOrDefault(r) : null,
            i.Payments.FirstOrDefault(x => x.ReversesPaymentId == p.Id)?.ReceiptNumber, p.Note, p.CreatedAt)).ToList();
        return new InvoiceDetail(i.Id, i.Number, i.StudentId, i.Student.AdmissionNumber, $"{i.Student.FirstName} {i.Student.LastName}", room,
            i.TermId, $"{i.Term.Number} Term", i.Term.Session.Name, [.. i.Lines.Select(l => new InvoiceLineDto(l.Description, l.Amount))],
            i.Total, i.AmountPaid, i.Balance, i.Status, i.DueOn, schedule, i.CreatedAt,
            payments, schedule.Count > 0, dueNow, Math.Max(0, dueNow - i.AmountPaid), i.CancelReason,
            // The least the next payment can be: the rest of the next instalment, or the whole balance without a plan.
            i.IsCancelled || i.Balance <= 0 ? 0
                : schedule.Count == 0 ? i.Balance
                : Math.Min(i.Balance, FeeRules.NextInstalmentMinimum([.. schedule.Select(s => s.Amount)], i.AmountPaid)));
    }

    public static ReceiptDto Receipt(Payment p, Invoice i, string? room) => new(
        p.Id, p.ReceiptNumber, p.Amount, p.Method, p.Reference, p.PaidOn, p.IsReversal, i.Id, i.Number, i.Student.AdmissionNumber,
        $"{i.Student.FirstName} {i.Student.LastName}", room, $"{i.Term.Number} Term {i.Term.Session.Name}", i.Balance);
}
