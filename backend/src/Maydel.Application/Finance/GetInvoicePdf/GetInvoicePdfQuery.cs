using Maydel.Application.Common.Messaging;
using Maydel.Application.Reports;

namespace Maydel.Application.Finance.GetInvoicePdf;

/// <summary>An invoice as an A4 PDF, with the student's admission number printed prominently.</summary>
public sealed record GetInvoicePdfQuery(Guid InvoiceId) : IQuery<PdfFile>;
