using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using Maydel.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Maydel.Infrastructure.Notifications;

/// <summary>No provider configured: emails wait in the outbox.</summary>
internal sealed class DisabledEmailSender : IEmailSender
{
    public bool IsEnabled => false;
    public Task SendAsync(EmailMessage message, CancellationToken ct) => throw new InvalidOperationException("Email is not configured.");
}

/// <summary>Development: writes each email as an .eml file you can open in any mail client. Nothing leaves the machine.</summary>
internal sealed class PickupEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public bool IsEnabled => true;

    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var folder = Path.GetFullPath(options.Value.PickupDirectory);
        Directory.CreateDirectory(folder);
        using var smtp = new SmtpClient { DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory, PickupDirectoryLocation = folder };
        using var mail = MailMessages.Build(message, options.Value);
        await smtp.SendMailAsync(mail, ct);
    }
}

/// <summary>Any SMTP server with STARTTLS (port 587), e.g. the school's mailbox or Brevo's SMTP relay.</summary>
internal sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public bool IsEnabled => true;

    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var o = options.Value;
        using var smtp = new SmtpClient(o.SmtpHost, o.SmtpPort)
        {
            EnableSsl = o.SmtpEnableSsl,
            Credentials = o.SmtpUserName is null ? null : new NetworkCredential(o.SmtpUserName, o.SmtpPassword),
        };
        using var mail = MailMessages.Build(message, o);
        await smtp.SendMailAsync(mail, ct);
    }
}

/// <summary>Resend's HTTP API (https://resend.com/docs/api-reference/emails/send-email).</summary>
internal sealed class ResendEmailSender(HttpClient http, IOptions<EmailOptions> options) : IEmailSender
{
    public bool IsEnabled => true;

    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var o = options.Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(new
            {
                from = message.From is { } f ? $"{f.Name} <{f.Address}>" : $"{o.FromName} <{o.FromAddress}>",
                to = new[] { message.To },
                reply_to = message.ReplyTo ?? o.ReplyTo,
                subject = message.Subject,
                text = message.TextBody,
                html = MailMessages.Html(message.TextBody),
                attachments = message.Attachments.Select(a => new { filename = a.FileName, content = Convert.ToBase64String(a.Content) }),
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.ResendApiKey);
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Resend returned {(int)response.StatusCode}: {(body.Length > 300 ? body[..300] : body)}");
        }
    }
}
