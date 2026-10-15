using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Finance.GetInvoice;

/// <summary>An invoice with its lines, payments so far and instalment schedule.</summary>
public sealed record GetInvoiceQuery(Guid Id) : IQuery<InvoiceDetail>;
