using System.Text.RegularExpressions;
using OpenSpeaker.Models;
namespace OpenSpeaker.Text;

public class RegexReplacer
{
    public string Replace(string message, IEnumerable<RegexReplacement> replacements)
    {
        foreach (var r in replacements.Where(x => x.Enabled).OrderBy(x => x.Order))
        {
            message = ApplySingle(message, r);
            if (string.IsNullOrEmpty(message)) return string.Empty;
        }
        return message;
    }

    public string ApplySingle(string message, RegexReplacement r)
    {
        if (string.IsNullOrEmpty(r.Pattern)) return message;
        var mode = string.IsNullOrEmpty(r.Mode) ? "Replace" : r.Mode;

        if (mode == "Skip")
        {
            bool skip;
            if (r.IsRegex)
            {
                try { skip = Regex.IsMatch(message, r.Pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)); }
                catch { return message; }
            }
            else if (r.WholeWord)
            {
                var wbPattern = $@"\b{Regex.Escape(r.Pattern)}\b";
                try { skip = Regex.IsMatch(message, wbPattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)); }
                catch { return message; }
            }
            else
            {
                skip = message.Contains(r.Pattern, StringComparison.OrdinalIgnoreCase);
            }
            return skip ? string.Empty : message;
        }
        else
        {
            if (r.IsRegex)
            {
                try { return Regex.Replace(message, r.Pattern, r.Replacement, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)); }
                catch { return message; }
            }
            else if (r.WholeWord)
            {
                var wbPattern = $@"\b{Regex.Escape(r.Pattern)}\b";
                try { return Regex.Replace(message, wbPattern, r.Replacement, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)); }
                catch { return message; }
            }
            else
            {
                return message.Replace(r.Pattern, r.Replacement, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
