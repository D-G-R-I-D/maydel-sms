using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Finance;
using Maydel.Application.Reports;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Portal.GetChildFeeDocument;

internal sealed class GetChildFeeDocumentHandler(IAppDbContext db, AccessRules access, FinanceDocuments documents)
    : IQueryHandler<GetChildFeeDocumentQuery, PdfFile>
{
    public async Task<Result<PdfFile>> Handle(GetChildFeeDocumentQuery q, CancellationToken ct)
    {
        if (!await access.PortalStudents().AnyAsync(s => s.Id == q.StudentId, ct)) return PortalErrors.StudentNotFound;
        if (q.PaymentId is { } paymentId)
        {
            var mine = await db.Payments.AnyAsync(p => p.Id == paymentId && p.Invoice.StudentId == q.StudentId && !p.Invoice.IsCancelled, ct);
            return mine && await documents.ReceiptAsync(paymentId, ct) is { } receipt ? receipt : PortalErrors.InvoiceNotFound;
        }
        var owned = q.InvoiceId is { } invoiceId && await db.Invoices.AnyAsync(i => i.Id == invoiceId && i.StudentId == q.StudentId && !i.IsCancelled, ct);
        return owned && await documents.InvoiceAsync(q.InvoiceId!.Value, ct) is { } pdf ? pdf : PortalErrors.InvoiceNotFound;
    }
}
