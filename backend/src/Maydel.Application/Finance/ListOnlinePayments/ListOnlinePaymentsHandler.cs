using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance.ListOnlinePayments;

internal sealed class ListOnlinePaymentsHandler(IAppDbContext db) : IQueryHandler<ListOnlinePaymentsQuery, PagedResult<OnlinePaymentRow>>
{
    public async Task<Result<PagedResult<OnlinePaymentRow>>> Handle(ListOnlinePaymentsQuery q, CancellationToken ct)
    {
        var rows = db.OnlinePayments.AsNoTracking().Where(o => q.Status == null || o.Status == q.Status)
            .Join(db.Invoices, o => o.InvoiceId, i => i.Id, (o, i) => new { o, i })
            .OrderByDescending(x => x.o.CreatedAt)
            .Select(x => new OnlinePaymentRow(x.o.Id, x.o.Reference, x.o.Status, x.o.Amount, x.o.Excess, x.i.Id, x.i.Number,
                x.i.Student.AdmissionNumber, x.i.Student.FirstName + " " + x.i.Student.LastName,
                db.Payments.Where(p => p.Id == x.o.PaymentId).Select(p => p.ReceiptNumber).FirstOrDefault(),
                x.o.Message, x.o.CreatedAt, x.o.CompletedAt));
        return await rows.ToPagedAsync(q.Page, q.PageSize, ct);
    }
}
