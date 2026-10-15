using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Finance;

namespace Maydel.Application.Finance.ListInvoices;

/// <summary>Invoices, newest first; filter by term, class, status, or search (name, admission number, invoice number).</summary>
public sealed record ListInvoicesQuery(
    Guid? TermId = null, Guid? ClassRoomId = null, InvoiceStatus? Status = null, string? Search = null, int? Page = null, int? PageSize = null)
    : IQuery<PagedResult<InvoiceSummary>>;
