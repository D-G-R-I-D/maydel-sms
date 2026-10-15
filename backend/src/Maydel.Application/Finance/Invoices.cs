using Maydel.Application.Abstractions;
using Maydel.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance;

internal static class Invoices
{
    public static InvoiceStatus StatusOf(bool cancelled, decimal total, decimal paid) =>
        cancelled ? InvoiceStatus.Cancelled : paid <= 0 ? InvoiceStatus.Unpaid : paid < total ? InvoiceStatus.PartiallyPaid : InvoiceStatus.Paid;

    public static async Task<InvoiceDetail?> DetailAsync(this IAppDbContext db, Guid id, CancellationToken ct)
    {
        var i = await db.Invoices.AsNoTracking().Include(x => x.Payments).Include(x => x.Student).Include(x => x.Term).ThenInclude(t => t.Session)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (i is null) return null;
        var room = await db.ClassRooms.AsNoTracking().Where(r => r.Id == i.ClassRoomId).Select(r => r.ClassLevel.Name + " " + r.Arm).FirstOrDefaultAsync(ct);
        var plan = await db.PlanForAsync(i.Term, ct);
        IReadOnlyList<ScheduleItem> schedule = [];
        if (plan is { Plan.Enabled: true } p && !i.IsCancelled)
        {
            var parts = p.Plan.Instalments.OrderBy(x => x.Order).ToList();
            var amounts = FeeRules.Split(i.Total, [.. parts.Select(x => x.Percent)]);
            schedule = [.. parts.Select((x, n) => new ScheduleItem(x.Order, x.Percent, amounts[n], x.DueOn))];
        }
        return new InvoiceDetail(i.Id, i.Number, i.StudentId, i.Student.AdmissionNumber, $"{i.Student.FirstName} {i.Student.LastName}", room,
            i.TermId, $"{i.Term.Number} Term", i.Term.Session.Name, [.. i.Lines.Select(l => new InvoiceLineDto(l.Description, l.Amount))],
            i.Total, i.AmountPaid, i.Balance, i.Status, i.DueOn, schedule, i.CreatedAt);
    }
}
