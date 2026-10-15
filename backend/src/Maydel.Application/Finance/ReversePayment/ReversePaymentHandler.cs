using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance.ReversePayment;

internal sealed class ReversePaymentHandler(IAppDbContext db, ISchoolClock clock, ICurrentUser me) : ICommandHandler<ReversePaymentCommand, ReceiptDto>
{
    public async Task<Result<ReceiptDto>> Handle(ReversePaymentCommand c, CancellationToken ct)
    {
        var invoiceId = await db.Payments.Where(p => p.Id == c.PaymentId).Select(p => (Guid?)p.InvoiceId).FirstOrDefaultAsync(ct);
        if (invoiceId is null) return FinanceErrors.PaymentNotFound;
        var invoice = await db.Invoices.Include(i => i.Payments).Include(i => i.Student).Include(i => i.Term).ThenInclude(t => t.Session)
            .FirstAsync(i => i.Id == invoiceId, ct);
        var original = invoice.Payments.Single(p => p.Id == c.PaymentId);

        var prefix = FeeRules.ReceiptNumber(invoice.Term.Session.Name, 0)[..^5];
        var number = FeeRules.ReceiptNumber(invoice.Term.Session.Name, await db.NextReceiptAsync(prefix, ct));
        var (reversal, error) = invoice.Reverse(original, c.Reason ?? "", clock.Today, me.UserId, number);
        if (error is not null) return error.ToError();
        db.Payments.Add(reversal!);
        await db.SaveChangesAsync(ct);

        var room = await db.ClassRooms.AsNoTracking().Where(r => r.Id == invoice.ClassRoomId).Select(r => r.ClassLevel.Name + " " + r.Arm).FirstOrDefaultAsync(ct);
        return Invoices.Receipt(reversal!, invoice, room);
    }
}
