using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using OpenSpeaker.Models;
namespace OpenSpeaker.Text;

public class RegexReplacer
{
    private static readonly ConcurrentDictionary<string, Regex> _compiled = new();

    private static Regex? GetRegex(string pattern)
    {
        try { return _compiled.GetOrAdd(pattern, p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1))); }
        catch { return null; }
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
                var rx = GetRegex(r.Pattern);
                if (rx == null) return message;
                try { skip = rx.IsMatch(message); }
                catch { return message; }
            }
            else if (r.WholeWord)
            {
                var rx = GetRegex($@"\b{Regex.Escape(r.Pattern)}\b");
                if (rx == null) return message;
                try { skip = rx.IsMatch(message); }
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
                var rx = GetRegex(r.Pattern);
                if (rx == null) return message;
                try { return rx.Replace(message, r.Replacement); }
                catch { return message; }
            }
            else if (r.WholeWord)
            {
                var rx = GetRegex($@"\b{Regex.Escape(r.Pattern)}\b");
                if (rx == null) return message;
                try { return rx.Replace(message, r.Replacement); }
                catch { return message; }
            }
            else
            {
                return message.Replace(r.Pattern, r.Replacement, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
