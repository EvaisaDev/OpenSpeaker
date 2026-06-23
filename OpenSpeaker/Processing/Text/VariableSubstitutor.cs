using System.Text.RegularExpressions;
namespace OpenSpeaker.Text;

public class VariableSubstitutor
{
    private static readonly Regex Placeholder = new(@"%(\w+)%", RegexOptions.Compiled);

    public string Substitute(string template, Dictionary<string, string> variables)
    {
        var lookup = new Dictionary<string, string>(variables, StringComparer.OrdinalIgnoreCase);
        return Placeholder.Replace(template, m =>
            lookup.TryGetValue(m.Groups[1].Value, out var value) ? value : m.Value);
    }
}
