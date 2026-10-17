using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Finance.PublishInvoices;

/// <summary>
/// Publishes a term's draft invoices (one class level, or all) so parents see them in the portal, and by default emails
/// each guardian with an address the invoice PDF and a portal link (E10.7). Already-published invoices are skipped, so
/// running it again only publishes (and notifies about) new ones.
/// </summary>
public sealed record PublishInvoicesCommand(Guid TermId, Guid? ClassLevelId = null, bool? NotifyParents = null) : ICommand<PublishInvoicesResult>;
