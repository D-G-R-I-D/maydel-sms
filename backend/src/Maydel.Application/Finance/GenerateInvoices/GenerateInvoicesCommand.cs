using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Finance.GenerateInvoices;

/// <summary>
/// Invoices every active student (of one class level, or all) for a term (E10.2): mandatory items for their level and
/// school-wide, plus optional items they opted in to, minus the sibling discount. Idempotent: students who already have
/// an invoice for the term are skipped, so it's safe to run again after new admissions. New-intake and returning
/// students get the items for their group (E10.6). Invoices start as drafts; publish them to show parents (E10.7).
/// </summary>
public sealed record GenerateInvoicesCommand(Guid TermId, Guid? ClassLevelId) : ICommand<GenerateInvoicesResult>;
