using System.Text;
using System.Text.RegularExpressions;
namespace OpenSpeaker.Infrastructure.Extensions;
public static class StringExtensions
{
    public static string TruncateTo(this string s, int maxLength) =>
        s.Length <= maxLength ? s : s[..maxLength];

    public static string ToSsmlEscaped(this string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");

    public static bool IsNullOrEmpty(this string? s) => string.IsNullOrEmpty(s);

    public static string RemoveExcessWhitespace(this string s) =>
        Regex.Replace(s.Trim(), @"\s+", " ");
}
