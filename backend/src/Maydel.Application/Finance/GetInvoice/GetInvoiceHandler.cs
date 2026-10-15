using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Finance.GetInvoice;

internal sealed class GetInvoiceHandler(IAppDbContext db, ISchoolClock clock) : IQueryHandler<GetInvoiceQuery, InvoiceDetail>
{
    public async Task<Result<InvoiceDetail>> Handle(GetInvoiceQuery q, CancellationToken ct) =>
        await db.DetailAsync(q.Id, clock.Today, ct) is { } detail ? detail : FinanceErrors.InvoiceNotFound;
}
