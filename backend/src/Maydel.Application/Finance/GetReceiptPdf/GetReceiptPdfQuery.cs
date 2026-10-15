using Maydel.Application.Common.Messaging;
using Maydel.Application.Reports;

namespace Maydel.Application.Finance.GetReceiptPdf;

/// <summary>A receipt (or reversal) as an A5 PDF. Reprints show the figures as they were when it was issued.</summary>
public sealed record GetReceiptPdfQuery(Guid PaymentId) : IQuery<PdfFile>;
