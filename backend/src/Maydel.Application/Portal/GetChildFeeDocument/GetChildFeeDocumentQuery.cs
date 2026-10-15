using Maydel.Application.Common.Messaging;
using Maydel.Application.Reports;

namespace Maydel.Application.Portal.GetChildFeeDocument;

/// <summary>An invoice PDF (<c>PaymentId</c> null) or a receipt PDF for the parent's child. Anything else is 404.</summary>
public sealed record GetChildFeeDocumentQuery(Guid StudentId, Guid? InvoiceId, Guid? PaymentId) : IQuery<PdfFile>;
