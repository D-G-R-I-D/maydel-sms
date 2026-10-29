using System.Net;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Ganss.Xss;

namespace Maydel.Application.Website;

/// <summary>
/// Cleans formatted content from the editor (posts, events, newsletters) to a small allow-list: headings, paragraphs,
/// emphasis, lists, quotes, links, rules, and images that live on our own media store. Scripts, styles, event handlers,
/// iframes and outside images are dropped. Also derives the plain-text version used for search, notices and email text.
/// </summary>
public static partial class RichText
{
    public const int MaxLength = 100_000;

    /// <summary>Images must come from the school's own store (uploaded through the editor).</summary>
    public const string ImagePrefix = "/api/public/site/images/";

    private static readonly HtmlSanitizer Sanitizer = Create();

    private static HtmlSanitizer Create()
    {
        var s = new HtmlSanitizer();
        Reset(s.AllowedTags, "p", "br", "h2", "h3", "h4", "strong", "b", "em", "i", "u", "s", "blockquote", "ul", "ol", "li", "a", "hr", "figure", "figcaption", "img");
        Reset(s.AllowedAttributes, "href", "src", "alt", "title", "width", "height", "class");
        Reset(s.AllowedSchemes, "http", "https", "mailto");
        Reset(s.UriAttributes, "href", "src");
        Reset(s.AllowedClasses, "button", "align-left", "align-center", "align-right", "wide");
        s.AllowedCssProperties.Clear();
        s.AllowedAtRules.Clear();
        s.FilterUrl += (_, e) =>
        {
            if (string.Equals(e.Tag?.TagName, "IMG", StringComparison.OrdinalIgnoreCase) && !e.OriginalUrl.StartsWith(ImagePrefix, StringComparison.Ordinal))
                e.SanitizedUrl = null;
        };
        s.PostProcessNode += (_, e) =>
        {
            if (e.Node is IElement { TagName: "A" } a) a.SetAttribute("rel", "noopener noreferrer");
        };
        return s;
    }

    private static void Reset(ISet<string> set, params string[] values)
    {
        set.Clear();
        foreach (var v in values) set.Add(v);
    }

    /// <summary>The cleaned HTML and its plain text; null when nothing is left.</summary>
    public static (string Html, string Text)? Clean(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var clean = Sanitizer.Sanitize(html).Trim();
        var text = ToText(clean);
        return text.Length == 0 && !clean.Contains("<img", StringComparison.OrdinalIgnoreCase) ? null : (clean, text);
    }

    /// <summary>Paragraphs and headings become blank-line separated, list items get bullets, tags go, entities are decoded.</summary>
    public static string ToText(string html)
    {
        var t = LineBreak().Replace(html, "\n");
        t = ListItem().Replace(t, "\n• ");
        t = BlockEnd().Replace(t, "\n\n");
        t = WebUtility.HtmlDecode(Tag().Replace(t, ""));
        t = string.Join("\n", t.Split('\n').Select(l => l.Trim()));
        return ManyBreaks().Replace(t, "\n\n").Trim();
    }

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreak();

    [GeneratedRegex(@"<li[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItem();

    [GeneratedRegex(@"</(p|h2|h3|h4|blockquote|figure|ul|ol)>|<hr\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockEnd();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ManyBreaks();
}
