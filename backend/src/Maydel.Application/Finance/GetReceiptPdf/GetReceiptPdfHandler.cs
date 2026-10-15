using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Reports;

namespace Maydel.Application.Finance.GetReceiptPdf;

internal sealed class GetReceiptPdfHandler(FinanceDocuments documents) : IQueryHandler<GetReceiptPdfQuery, PdfFile>
{
    public async Task<Result<PdfFile>> Handle(GetReceiptPdfQuery q, CancellationToken ct) =>
        await documents.ReceiptAsync(q.PaymentId, ct) is { } pdf ? pdf : FinanceErrors.PaymentNotFound;
}
