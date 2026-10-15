using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Maydel.Application.Abstractions;

namespace Maydel.Infrastructure.Notifications;

/// <summary>Builds a plain-text + simple HTML message (both from the same text, HTML-escaped).</summary>
internal static class MailMessages
{
    public static MailMessage Build(EmailMessage message, EmailOptions options)
    {
        var mail = new MailMessage
        {
            From = new MailAddress(options.FromAddress, options.FromName),
            Subject = message.Subject,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            Body = message.TextBody,
        };
        mail.To.Add(new MailAddress(message.To, message.ToName));
        if (!string.IsNullOrWhiteSpace(options.ReplyTo)) mail.ReplyToList.Add(options.ReplyTo);
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(Html(message.TextBody), Encoding.UTF8, MediaTypeNames.Text.Html));
        foreach (var a in message.Attachments)
            mail.Attachments.Add(new Attachment(new MemoryStream(a.Content), a.FileName, a.ContentType));
        return mail;
    }

    /// <summary>Paragraphs from blank lines, line breaks kept; everything escaped, so nothing in a name can inject markup.</summary>
    public static string Html(string text)
    {
        var paragraphs = text.Replace("\r\n", "\n").Split("\n\n")
            .Select(p => "<p>" + string.Join("<br>", p.Split('\n').Select(WebUtility.HtmlEncode)) + "</p>");
        return "<!doctype html><html><body style=\"font-family:Arial,sans-serif;font-size:14px;color:#1d2733\">"
            + string.Concat(paragraphs) + "</body></html>";
    }
}
