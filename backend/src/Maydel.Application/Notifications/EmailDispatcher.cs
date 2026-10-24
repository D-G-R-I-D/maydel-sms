using Maydel.Application.Abstractions;
using Maydel.Application.Reports;
using Maydel.Domain.Assessment;
using Maydel.Domain.Communication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Maydel.Application.Notifications;

/// <summary>
/// Sends due emails from the outbox (E12.3). Report emails get the PDF generated at send time, and only while the report
/// is still published, so an unpublished draft can never be mailed. One email failing never stops the rest.
/// </summary>
public sealed class EmailDispatcher(
    IAppDbContext db, IEmailSender sender, ReportBuilder reports, ReportPdfs pdfs, Finance.FinanceDocuments finance, Admissions.AdmissionLetters letters,
    SchoolSettings.SchoolBranding branding,
    Microsoft.Extensions.Options.IOptions<EmailOptions> options, TimeProvider clock, ILogger<EmailDispatcher> logger)
{
    public const int BatchSize = 10;

    /// <summary>Sends up to <see cref="BatchSize"/> due emails; returns how many were attempted.</summary>
    public async Task<int> DispatchDueAsync(CancellationToken ct)
    {
        if (!sender.IsEnabled) return 0;
        var now = clock.GetUtcNow();
        // School notices go first; newsletters use what's left of the provider's daily allowance and otherwise wait.
        var newsletterSlots = int.MaxValue;
        if (options.Value.DailyLimit > 0)
        {
            var since = now.AddHours(-24);
            newsletterSlots = Math.Max(0, options.Value.DailyLimit - await db.OutboxEmails.CountAsync(e => e.Status == EmailStatus.Sent && e.SentAt > since, ct));
        }
        var pending = db.OutboxEmails.Where(e => e.Status == EmailStatus.Pending && e.NextAttemptAt <= now);
        if (newsletterSlots == 0) pending = pending.Where(e => e.Kind != EmailKind.Newsletter);
        var due = await pending
            .OrderBy(e => e.Kind == EmailKind.Newsletter ? 1 : 0).ThenBy(e => e.NextAttemptAt).ThenBy(e => e.CreatedAt)
            .Take(BatchSize).ToListAsync(ct);
        var newsletterCopies = due.Where(e => e.Kind == EmailKind.Newsletter).Skip(newsletterSlots).ToHashSet();
        due = [.. due.Where(e => !newsletterCopies.Contains(e))];
        var issues = new Dictionary<Guid, Newsletter?>();

        var brand = due.Count == 0 ? null : await BrandAsync(ct);
        foreach (var email in due)
        {
            try
            {
                var attachments = new List<EmailAttachment>();
                if (email.Kind == EmailKind.ReportPublished)
                {
                    if (await ReportCardAsync(email, ct) is not { } card)
                    {
                        email.MarkSkipped("The report is no longer published.");
                        await db.SaveChangesAsync(ct);
                        continue;
                    }
                    attachments.Add(new EmailAttachment(ReportPdfs.FileName(card), "application/pdf", await pdfs.RenderAsync([card], ct)));
                }
                else if (email.Kind is EmailKind.ProofConfirmed or EmailKind.PaymentReceipt && email.PaymentId is { } paymentId)
                {
                    if (await finance.ReceiptAsync(paymentId, ct) is { } receipt)
                        attachments.Add(new EmailAttachment(receipt.FileName, "application/pdf", receipt.Content));
                }
                else if (email.Kind == EmailKind.InvoicePublished)
                {
                    // Only while the invoice is still live and published: a cancelled invoice is never mailed.
                    var live = email.InvoiceId is { } invoiceId
                        && await db.Invoices.AnyAsync(i => i.Id == invoiceId && !i.IsCancelled && i.PublishedAt != null, ct);
                    if (!live || await finance.InvoiceAsync(email.InvoiceId!.Value, ct) is not { } pdf)
                    {
                        email.MarkSkipped("The invoice is no longer published.");
                        await db.SaveChangesAsync(ct);
                        continue;
                    }
                    attachments.Add(new EmailAttachment(pdf.FileName, "application/pdf", pdf.Content));
                }

                else if (email.Kind == EmailKind.AdmissionOffer)
                {
                    // Only while the offer stands: a withdrawn offer is never mailed.
                    if (email.ApplicationId is not { } applicationId || await letters.LetterAsync(applicationId, ct) is not { } letter)
                    {
                        email.MarkSkipped("The offer no longer stands.");
                        await db.SaveChangesAsync(ct);
                        continue;
                    }
                    attachments.Add(new EmailAttachment(letter.FileName, "application/pdf", letter.Content));
                }

                string? html = null;
                EmailUnsubscribe? unsubscribe = null;
                string? preheader = null;
                var text = email.Body;
                if (email.Kind == EmailKind.Newsletter && email.NewsletterId is { } issueId)
                {
                    if (!issues.TryGetValue(issueId, out var issue))
                        issues[issueId] = issue = await db.Newsletters.AsNoTracking().FirstOrDefaultAsync(n => n.Id == issueId, ct);
                    if (issue is null)
                    {
                        email.MarkSkipped("The newsletter was deleted.");
                        await db.SaveChangesAsync(ct);
                        continue;
                    }
                    (html, preheader) = (NewsletterHtml.Absolute(issue.BodyHtml, options.Value), issue.Preheader);
                    if (email.UnsubscribeToken is { } token)
                    {
                        unsubscribe = NewsletterHtml.Unsubscribe(token, options.Value);
                        text = $"{email.Body}\n\nUnsubscribe from the newsletter: {unsubscribe.PageUrl}";
                    }
                }

                await sender.SendAsync(new EmailMessage(email.ToAddress, email.ToName, email.Subject, text, attachments,
                    Senders.For(email.Kind, options.Value), email.ReplyTo, brand! with { Heading = EmailHeadings.For(email.Kind) }, html, unsubscribe, preheader), ct);
                email.MarkSent(clock.GetUtcNow());
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Email {EmailId} attempt {Attempt} failed", email.Id, email.Attempts + 1);
                email.MarkFailed($"{ex.GetType().Name}: {ex.Message}", clock.GetUtcNow());
            }
            await db.SaveChangesAsync(ct);
        }
        return due.Count;
    }

    private async Task<ReportCard?> ReportCardAsync(OutboxEmail email, CancellationToken ct)
    {
        if (email.StudentId is not { } studentId || email.TermId is not { } termId) return null;
        var classRoomId = await db.TermReports.AsNoTracking()
            .Where(r => r.StudentId == studentId && r.TermId == termId && r.Status == ReportStatus.Published)
            .Select(r => (Guid?)r.ClassRoomId).FirstOrDefaultAsync(ct);
        return classRoomId is null ? null : await reports.BuildAsync(studentId, termId, classRoomId.Value, ct);
    }

    /// <summary>The school's name, contacts and logo for the email layout (a logo over 300 KB is left out; the crest is used).</summary>
    private async Task<EmailBrand> BrandAsync(CancellationToken ct)
    {
        var b = await branding.LoadAsync(ct);
        return new EmailBrand(b.Name, b.Motto, string.IsNullOrWhiteSpace(b.Address) ? null : b.Address, b.Phone, b.Email, b.Website,
            b.Logo is { Length: <= 300_000 } logo ? logo : null, null);
    }
}
