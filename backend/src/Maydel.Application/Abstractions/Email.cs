namespace Maydel.Application.Abstractions;

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

/// <summary>
/// <c>From</c>/<c>ReplyTo</c>: null = the configured defaults (E12.7 picks a department sender per email kind).
/// <c>Brand</c>: the school's look for the HTML part; null = a plain layout.
/// </summary>
public sealed record EmailMessage(string To, string? ToName, string Subject, string TextBody, IReadOnlyList<EmailAttachment> Attachments,
    EmailFrom? From = null, string? ReplyTo = null, EmailBrand? Brand = null);

/// <summary>
/// The school's identity for branded emails. <c>Heading</c> titles the message ("Payment received"); <c>Logo</c> is PNG or JPEG
/// bytes embedded in the email (no remote images to block). Null logo = the built-in crest.
/// </summary>
public sealed record EmailBrand(string School, string? Motto, string? Address, string? Phone, string? Email, string? Website, byte[]? Logo, string? Heading);

public sealed record EmailFrom(string Address, string Name);

/// <summary>Delivers one email. Throws on failure; the outbox records the error and retries (E12.3).</summary>
public interface IEmailSender
{
    /// <summary>False when no provider is configured: emails stay queued until one is.</summary>
    bool IsEnabled { get; }

    Task SendAsync(EmailMessage message, CancellationToken ct);
}

/// <summary>
/// <c>Email</c> configuration. Provider: <c>None</c> (queue only), <c>Pickup</c> (write .eml files, for development),
/// <c>Smtp</c> or <c>Resend</c>. Secrets (SMTP password, Resend API key) come from the environment, never appsettings.
/// </summary>
public sealed class EmailOptions
{
    public const string Section = "Email";

    public string Provider { get; set; } = "None";
    public string FromAddress { get; set; } = "no-reply@maydelspringschools.com";
    public string FromName { get; set; } = "Maydel Spring Schools";
    public string? ReplyTo { get; set; }

    /// <summary>
    /// Department senders (E12.7), e.g. admissions@ / accounts@ / results@. Each must be verified with the email provider
    /// (same domain as <see cref="FromAddress"/>). Empty = use <see cref="FromAddress"/>.
    /// </summary>
    public string? AdmissionsAddress { get; set; }
    public string? AccountsAddress { get; set; }
    public string? ResultsAddress { get; set; }

    /// <summary>Link to the parent portal mentioned in result emails, e.g. https://portal.maydelspringschools.com.</summary>
    public string? PortalUrl { get; set; }

    public string PickupDirectory { get; set; } = "emails";
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUserName { get; set; }
    public string? SmtpPassword { get; set; }
    public bool SmtpEnableSsl { get; set; } = true;
    public string? ResendApiKey { get; set; }

    /// <summary>How often the worker looks for due emails.</summary>
    public int PollSeconds { get; set; } = 15;
}
