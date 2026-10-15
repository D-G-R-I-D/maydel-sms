using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance.RecordPayment;

internal sealed class RecordPaymentHandler(IAppDbContext db, ISchoolClock clock, ICurrentUser me) : ICommandHandler<RecordPaymentCommand, ReceiptDto>
{
    public async Task<Result<ReceiptDto>> Handle(RecordPaymentCommand c, CancellationToken ct)
    {
        var invoice = await db.Invoices.Include(i => i.Payments).Include(i => i.Student).Include(i => i.Term).ThenInclude(t => t.Session)
            .FirstOrDefaultAsync(i => i.Id == c.InvoiceId, ct);
        if (invoice is null) return FinanceErrors.InvoiceNotFound;
        var paidOn = c.PaidOn ?? clock.Today;
        if (paidOn > clock.Today) return FinanceErrors.FuturePayment;

        var prefix = FeeRules.ReceiptNumber(invoice.Term.Session.Name, 0)[..^5];
        var number = FeeRules.ReceiptNumber(invoice.Term.Session.Name, await db.NextReceiptAsync(prefix, ct));
        var schedule = await db.ScheduleAsync(invoice.Term, invoice.Total, ct);
        var (payment, error) = invoice.RecordPayment(c.Amount, c.Method, c.Reference, paidOn, me.UserId, number, [.. schedule.Select(s => s.Amount)]);
        if (error is not null) return error.ToError();
        db.Payments.Add(payment!);
        await db.SaveChangesAsync(ct);

        var room = await db.ClassRooms.AsNoTracking().Where(r => r.Id == invoice.ClassRoomId).Select(r => r.ClassLevel.Name + " " + r.Arm).FirstOrDefaultAsync(ct);
        return Invoices.Receipt(payment!, invoice, room);
    }
}
