using System.Text.RegularExpressions;
using OpenSpeaker.Models;
namespace OpenSpeaker.Text;

public class RegexReplacer
{
    public string Replace(string message, IEnumerable<RegexReplacement> replacements)
    {
        foreach (var r in replacements.Where(x => x.Enabled).OrderBy(x => x.Order))
        {
            if (string.IsNullOrEmpty(r.Pattern)) continue;
            var mode = string.IsNullOrEmpty(r.Mode) ? "Replace" : r.Mode;

            if (mode == "Skip")
            {
                bool skip;
                if (r.IsRegex)
                {
                    try { skip = Regex.IsMatch(message, r.Pattern, RegexOptions.None, TimeSpan.FromSeconds(1)); }
                    catch { continue; }
                }
                else
                {
                    skip = message.Contains(r.Pattern, StringComparison.OrdinalIgnoreCase);
                }
                if (skip) return string.Empty;
            }
            else
            {
                if (r.IsRegex)
                {
                    try { message = Regex.Replace(message, r.Pattern, r.Replacement, RegexOptions.None, TimeSpan.FromSeconds(1)); }
                    catch { }
                }
                else
                {
                    message = message.Replace(r.Pattern, r.Replacement, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        return message;
    }
}
