using Maydel.Application.Abstractions;

namespace Maydel.Application.Notifications;

/// <summary>Turns a newsletter's site-relative links and pictures into full addresses an inbox can open.</summary>
public static class NewsletterHtml
{
    public static string Absolute(string html, EmailOptions o)
    {
        var api = (o.PublicApiUrl ?? o.PortalUrl ?? "").TrimEnd('/');
        var portal = (o.PortalUrl ?? "").TrimEnd('/');
        return html.Replace("src=\"/api/public/", $"src=\"{api}/api/public/", StringComparison.Ordinal)
            .Replace("href=\"/", $"href=\"{portal}/", StringComparison.Ordinal);
    }

    public static EmailUnsubscribe Unsubscribe(string token, EmailOptions o) => new(
        $"{(o.PortalUrl ?? "").TrimEnd('/')}/newsletter/unsubscribe?t={token}",
        string.IsNullOrWhiteSpace(o.PublicApiUrl) ? null : $"{o.PublicApiUrl.TrimEnd('/')}/api/public/newsletter/unsubscribe?t={token}");
}
