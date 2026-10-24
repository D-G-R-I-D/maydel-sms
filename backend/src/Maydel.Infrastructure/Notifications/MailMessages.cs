using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;
using Maydel.Application.Abstractions;

namespace Maydel.Infrastructure.Notifications;

/// <summary>
/// Builds the plain-text and HTML parts of an email from one text body. The HTML is the school's branded layout:
/// "Label: value" lines become a details table and links become buttons. Everything from the text is HTML-escaped,
/// so nothing in a name can inject markup.
/// </summary>
internal static partial class MailMessages
{
    public const string LogoContentId = "school-logo";

    public static MailMessage Build(EmailMessage message, EmailOptions options)
    {
        var mail = new MailMessage
        {
            From = message.From is { } from ? new MailAddress(from.Address, from.Name) : new MailAddress(options.FromAddress, options.FromName),
            Subject = message.Subject,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            Body = message.TextBody,
        };
        mail.To.Add(new MailAddress(message.To, message.ToName));
        if ((message.ReplyTo ?? options.ReplyTo) is { Length: > 0 } replyTo) mail.ReplyToList.Add(replyTo);
        if (message.Unsubscribe?.OneClickUrl is { } oneClick)
        {
            mail.Headers.Add("List-Unsubscribe", $"<{oneClick}>");
            mail.Headers.Add("List-Unsubscribe-Post", "List-Unsubscribe=One-Click");
        }
        var html = AlternateView.CreateAlternateViewFromString(Html(message), Encoding.UTF8, MediaTypeNames.Text.Html);
        if (message.Brand is not null && Logo(message.Brand) is { } logo)
            html.LinkedResources.Add(new LinkedResource(new MemoryStream(logo.Bytes), logo.ContentType) { ContentId = LogoContentId });
        mail.AlternateViews.Add(html);
        foreach (var a in message.Attachments)
            mail.Attachments.Add(new Attachment(new MemoryStream(a.Content), a.FileName, a.ContentType));
        return mail;
    }

    /// <summary>The logo to embed: the school's own, else the built-in crest.</summary>
    public static (byte[] Bytes, string ContentType)? Logo(EmailBrand brand)
    {
        if (brand.Logo is { Length: > 3 } own)
            return (own, own[0] == 0xFF && own[1] == 0xD8 ? MediaTypeNames.Image.Jpeg : "image/png");
        using var stream = typeof(MailMessages).Assembly.GetManifestResourceStream("Maydel.ReportLogo.png");
        if (stream is null) return null;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return (buffer.ToArray(), "image/png");
    }

    // Brand palette (docs/05-design-system.md), inlined because email clients ignore stylesheets.
    private const string Navy = "#234a6d", Gold = "#d9b16a", Text = "#1f2c38", Muted = "#586d7d", Page = "#f7f9fb", Line = "#d6e1ec", Fill = "#ebf1f7";
    private const string Sans = "Arial,Helvetica,sans-serif", Serif = "Georgia,'Times New Roman',serif";

    public static string Html(EmailMessage m) => Html(m.TextBody, m.Brand, m.HtmlBody, m.Unsubscribe, m.Preheader);

    public static string Html(string text, EmailBrand? brand = null, string? htmlBody = null, EmailUnsubscribe? unsubscribe = null, string? preheader = null)
    {
        var body = new StringBuilder();
        if (htmlBody is not null) body.Append(Styled(htmlBody));
        else
            foreach (var block in text.Replace("\r\n", "\n").Split("\n\n").Select(b => b.Trim('\n')).Where(b => b.Trim().Length > 0))
                body.Append(Block(block));
        if (brand is null)
            return $"<!doctype html><html><body style=\"font-family:{Sans};font-size:14px;color:{Text}\">{body}</body></html>";

        var school = E(brand.School);
        var heading = brand.Heading is { Length: > 0 } h ? $"<h1 style=\"margin:0 0 16px;font-family:{Serif};font-size:22px;font-weight:normal;color:{Navy}\">{E(h)}</h1>" : "";
        var motto = brand.Motto is { Length: > 0 } m ? $"<div style=\"font-family:{Sans};font-size:12px;letter-spacing:.5px;color:{Gold};margin-top:4px\">{E(m)}</div>" : "";
        var contacts = string.Join(" &middot; ", new[] { brand.Phone, brand.Email, brand.Website }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => E(x!)));
        return $"""
            <!doctype html>
            <html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{school}</title></head>
            <body style="margin:0;padding:0;background:{Page}">{(preheader is { Length: > 0 } ph ? $"<div style=\"display:none;max-height:0;overflow:hidden;opacity:0\">{E(ph)}</div>" : "")}
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:{Page}"><tr><td align="center" style="padding:24px 12px">
            <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="width:100%;max-width:600px;background:#ffffff;border:1px solid {Line};border-radius:8px;overflow:hidden">
            <tr><td style="background:{Navy};padding:20px 28px">
              <table role="presentation" cellpadding="0" cellspacing="0"><tr>
                <td style="padding-right:14px;vertical-align:middle"><img src="cid:{LogoContentId}" width="52" height="52" alt="" style="display:block;border:0;border-radius:6px;background:#ffffff"></td>
                <td style="vertical-align:middle"><div style="font-family:{Serif};font-size:20px;color:#ffffff">{school}</div>{motto}</td>
              </tr></table>
            </td></tr>
            <tr><td style="height:4px;background:{Gold};font-size:0;line-height:0">&nbsp;</td></tr>
            <tr><td style="padding:28px;font-family:{Sans};font-size:15px;line-height:1.6;color:{Text}">{heading}{body}</td></tr>
            <tr><td style="padding:18px 28px;background:{Fill};font-family:{Sans};font-size:12px;line-height:1.6;color:{Muted}">
              <strong style="color:{Navy}">{school}</strong>{(brand.Address is { Length: > 0 } a ? "<br>" + E(a) : "")}{(contacts.Length > 0 ? "<br>" + contacts : "")}
              <br>You're receiving this because you're a parent, guardian, student, staff member, subscriber or applicant at {school}.{(unsubscribe is null ? "" : $"<br><a href=\"{E(unsubscribe.PageUrl)}\" style=\"color:{Muted}\">Unsubscribe from the newsletter</a>")}
            </td></tr>
            </table>
            </td></tr></table>
            </body></html>
            """;
    }

    /// <summary>
    /// Inline styles for cleaned editor HTML (it carries no style attributes of its own, so adding them is safe): email apps
    /// ignore stylesheets.
    /// </summary>
    private static string Styled(string html)
    {
        // Alignment classes become text-align; the class itself stays (harmless).
        html = Aligned().Replace(html, m => $"<{m.Groups[1].Value} class=\"align-{m.Groups[2].Value}\" style=\"text-align:{m.Groups[2].Value}\">");
        html = ButtonLink().Replace(html, m => m.Value.TrimEnd('>')
            + $" style=\"display:inline-block;background:{Navy};color:#ffffff;text-decoration:none;font-weight:bold;padding:12px 22px;border-radius:6px\">");
        html = PlainLink().Replace(html, $"<a style=\"color:{Navy}\" ");
        return html
            .Replace("<h2>", $"<h2 style=\"margin:24px 0 8px;font-family:{Serif};font-size:20px;font-weight:normal;color:{Navy}\">")
            .Replace("<h3>", $"<h3 style=\"margin:20px 0 6px;font-size:17px;color:{Navy}\">")
            .Replace("<h4>", $"<h4 style=\"margin:16px 0 4px;font-size:15px;color:{Navy}\">")
            .Replace("<p>", "<p style=\"margin:0 0 14px\">")
            .Replace("<blockquote>", $"<blockquote style=\"margin:0 0 16px;padding:8px 16px;border-left:4px solid {Gold};color:{Muted}\">")
            .Replace("<hr>", $"<hr style=\"border:0;border-top:1px solid {Line};margin:20px 0\">")
            .Replace("<figcaption>", $"<figcaption style=\"font-size:13px;color:{Muted};margin-top:6px\">")
            .Replace("<figure>", "<figure style=\"margin:0 0 16px\">")
            .Replace("<img ", "<img style=\"max-width:100%;height:auto;border-radius:6px;display:block\" ");
    }

    [GeneratedRegex("<(p|h2|h3|h4|figure) class=\"align-(left|center|right)\">")]
    private static partial Regex Aligned();

    /// <summary>An editor link marked as a button (no style attribute yet: cleaned HTML never has one).</summary>
    [GeneratedRegex("<a [^>]*class=\"button\"[^>]*>")]
    private static partial Regex ButtonLink();

    /// <summary>Any other link: one without a style attribute.</summary>
    [GeneratedRegex("<a (?![^>]*style=)")]
    private static partial Regex PlainLink();

    /// <summary>One paragraph of the text: a details table, a link button, or a plain paragraph with line breaks.</summary>
    private static string Block(string block)
    {
        var lines = block.Split('\n');
        if (lines.Length >= 1 && lines.All(l => Detail().IsMatch(l)))
        {
            var rows = lines.Select(l => Detail().Match(l)).Select(m =>
                $"<tr><td style=\"padding:8px 12px;border-bottom:1px solid {Line};color:{Muted};white-space:nowrap\">{E(m.Groups[1].Value)}</td>"
                + $"<td style=\"padding:8px 12px;border-bottom:1px solid {Line};font-weight:bold;text-align:right\">{E(m.Groups[2].Value)}</td></tr>");
            return $"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin:0 0 16px;border:1px solid {Line};border-radius:6px;border-collapse:separate;font-size:14px\">{string.Concat(rows)}</table>";
        }
        if (lines.Length == 1 && Link().Match(block) is { Success: true } link && link.Index > 0)
        {
            var url = link.Value.TrimEnd('.', ',', ')');
            var lead = block[..link.Index].TrimEnd(' ', ':');
            return $"<p style=\"margin:0 0 12px\">{E(lead)}.</p>"
                + $"<p style=\"margin:0 0 20px\"><a href=\"{E(url)}\" style=\"display:inline-block;background:{Navy};color:#ffffff;text-decoration:none;font-weight:bold;padding:12px 22px;border-radius:6px\">{E(ButtonLabel(url))}</a></p>";
        }
        return $"<p style=\"margin:0 0 16px\">{string.Join("<br>", lines.Select(Linkify))}</p>";
    }

    private static string ButtonLabel(string url) =>
        url.Contains("/portal", StringComparison.OrdinalIgnoreCase) ? "Open the parent portal"
        : url.Contains("/newsletter/confirm", StringComparison.OrdinalIgnoreCase) ? "Confirm my subscription"
        : url.Contains("/admissions", StringComparison.OrdinalIgnoreCase) ? "Open your application"
        : "Open the link";

    /// <summary>Escapes a line and turns any http(s) address in it into a link.</summary>
    private static string Linkify(string line)
    {
        var html = new StringBuilder();
        var at = 0;
        foreach (Match m in Link().Matches(line))
        {
            html.Append(E(line[at..m.Index]));
            var url = m.Value.TrimEnd('.', ',', ')');
            html.Append($"<a href=\"{E(url)}\" style=\"color:{Navy}\">{E(url)}</a>").Append(E(m.Value[url.Length..]));
            at = m.Index + m.Length;
        }
        return html.Append(E(line[at..])).ToString();
    }

    private static string E(string s) => WebUtility.HtmlEncode(s);

    /// <summary>"Invoice: INV-001", "Due by: 1 May 2027": a short label, a colon, a value (not a URL or a sentence).</summary>
    [GeneratedRegex(@"^([A-Z][A-Za-z ()/'-]{1,28}):\s+(\S.{0,80})$")]
    private static partial Regex Detail();

    [GeneratedRegex(@"https?://[^\s<>""]+")]
    private static partial Regex Link();
}
