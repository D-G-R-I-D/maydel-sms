using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance.AdjustInvoice;

internal sealed class AdjustInvoiceHandler(IAppDbContext db, ISchoolClock clock) : ICommandHandler<AdjustInvoiceCommand, InvoiceDetail>
{
    public async Task<Result<InvoiceDetail>> Handle(AdjustInvoiceCommand c, CancellationToken ct)
    {
        var invoice = await db.Invoices.Include(i => i.Payments).FirstOrDefaultAsync(i => i.Id == c.InvoiceId, ct);
        if (invoice is null) return FinanceErrors.InvoiceNotFound;
        if (invoice.Adjust(c.Description ?? "", c.Amount) is { } invalid) return invalid.ToError();
        await db.SaveChangesAsync(ct);
        return (await db.DetailAsync(invoice.Id, clock.Today, ct))!;
    }
}
