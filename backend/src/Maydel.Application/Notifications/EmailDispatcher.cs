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
    IAppDbContext db, IEmailSender sender, ReportBuilder reports, ReportPdfs pdfs, TimeProvider clock, ILogger<EmailDispatcher> logger)
{
    public const int BatchSize = 10;

    /// <summary>Sends up to <see cref="BatchSize"/> due emails; returns how many were attempted.</summary>
    public async Task<int> DispatchDueAsync(CancellationToken ct)
    {
        if (!sender.IsEnabled) return 0;
        var now = clock.GetUtcNow();
        var due = await db.OutboxEmails
            .Where(e => e.Status == EmailStatus.Pending && e.NextAttemptAt <= now)
            .OrderBy(e => e.NextAttemptAt).ThenBy(e => e.CreatedAt)
            .Take(BatchSize).ToListAsync(ct);

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

                await sender.SendAsync(new EmailMessage(email.ToAddress, email.ToName, email.Subject, email.Body, attachments), ct);
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
}
