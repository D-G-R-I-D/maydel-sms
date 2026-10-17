using System.Text;

namespace Maydel.Api.Common;

public static class Csv
{
    /// <summary>A spreadsheet-safe cell: values starting with = + - @ are prefixed so Excel never runs them as formulas.</summary>
    public static string Cell(string? v)
    {
        v ??= "";
        if (v.Length > 0 && "=+-@\t\r".Contains(v[0])) v = "'" + v;
        return v.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    }

    /// <summary>UTF-8 with a BOM so Excel reads ₦ and accents correctly.</summary>
    public static byte[] Bytes(StringBuilder csv) => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
}
