using Maydel.Domain.Common;

namespace Maydel.Domain.Communication;

public enum EmailKind
{
    /// <summary>A guardian's copy of a published report card (PDF attached at send time).</summary>
    ReportPublished,

    /// <summary>A guardian's copy of a newly published fees invoice (PDF attached at send time).</summary>
    InvoicePublished,

    /// <summary>Proof of payment confirmed: the receipt PDF is attached at send time.</summary>
    ProofConfirmed,

    /// <summary>Proof of payment rejected, with the bursar's reason. No attachment.</summary>
    ProofRejected,
}

public enum EmailStatus
{
    Pending,
    Sent,
    /// <summary>Gave up after <see cref="OutboxEmail.MaxAttempts"/> tries; an admin can retry.</summary>
    Failed,
    /// <summary>No longer relevant (e.g. the report was unpublished or published again); never sent.</summary>
    Skipped,
}

/// <summary>
/// An email waiting to be sent (E12.3 outbox). Written in the same transaction as the change that triggers it, then sent
/// by a background worker with retries, so a provider outage never loses or blocks the change itself.
/// </summary>
public class OutboxEmail : Entity
{
    public const int MaxAttempts = 5;

    public EmailKind Kind { get; set; }
    public required string ToAddress { get; set; }
    public string? ToName { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }

    public Guid? StudentId { get; set; }
    public Guid? TermId { get; set; }
    public Guid? GuardianId { get; set; }
    public Guid? InvoiceId { get; set; }
    public Guid? PaymentId { get; set; }

    public EmailStatus Status { get; set; } = EmailStatus.Pending;
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public string? LastError { get; set; }

    /// <summary>Wait before the next try: 1, 2, 4, 8 minutes.</summary>
    public static TimeSpan Backoff(int attempts) => TimeSpan.FromMinutes(Math.Pow(2, Math.Max(0, attempts - 1)));

    public void MarkSent(DateTimeOffset now) => (Status, SentAt, LastError) = (EmailStatus.Sent, now, null);

    public void MarkSkipped(string reason) => (Status, LastError) = (EmailStatus.Skipped, reason);

    public void MarkFailed(string error, DateTimeOffset now)
    {
        Attempts++;
        LastError = error.Length > 500 ? error[..500] : error;
        if (Attempts >= MaxAttempts) Status = EmailStatus.Failed;
        else NextAttemptAt = now + Backoff(Attempts);
    }

    /// <summary>An admin asks for another go after it failed for good.</summary>
    public DomainError? Retry(DateTimeOffset now)
    {
        if (Status != EmailStatus.Failed) return new("email.not_failed", "Only failed emails can be retried.");
        (Status, Attempts, NextAttemptAt) = (EmailStatus.Pending, 0, now);
        return null;
    }
}
