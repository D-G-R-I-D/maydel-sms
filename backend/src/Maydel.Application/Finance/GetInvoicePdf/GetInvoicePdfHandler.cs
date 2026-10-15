using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Reports;

namespace Maydel.Application.Finance.GetInvoicePdf;

internal sealed class GetInvoicePdfHandler(FinanceDocuments documents) : IQueryHandler<GetInvoicePdfQuery, PdfFile>
{
    public async Task<Result<PdfFile>> Handle(GetInvoicePdfQuery q, CancellationToken ct) =>
        await documents.InvoiceAsync(q.InvoiceId, ct) is { } pdf ? pdf : FinanceErrors.InvoiceNotFound;
}
