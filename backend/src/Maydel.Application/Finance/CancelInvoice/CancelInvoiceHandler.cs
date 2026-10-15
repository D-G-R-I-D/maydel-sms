using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance.CancelInvoice;

internal sealed class CancelInvoiceHandler(IAppDbContext db, ISchoolClock clock, TimeProvider time) : ICommandHandler<CancelInvoiceCommand, InvoiceDetail>
{
    public async Task<Result<InvoiceDetail>> Handle(CancelInvoiceCommand c, CancellationToken ct)
    {
        var invoice = await db.Invoices.Include(i => i.Payments).FirstOrDefaultAsync(i => i.Id == c.InvoiceId, ct);
        if (invoice is null) return FinanceErrors.InvoiceNotFound;
        if (invoice.Cancel(c.Reason ?? "", time.GetUtcNow()) is { } invalid) return invalid.ToError();
        await db.SaveChangesAsync(ct);
        return (await db.DetailAsync(invoice.Id, clock.Today, ct))!;
    }
}
