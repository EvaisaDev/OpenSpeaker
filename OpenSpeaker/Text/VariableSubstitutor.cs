namespace OpenSpeaker.Text;

public class VariableSubstitutor
{
    public string Substitute(string template, Dictionary<string, string> variables)
    {
        foreach (var kv in variables)
            template = template.Replace($"%{kv.Key}%", kv.Value, StringComparison.OrdinalIgnoreCase);
        return template;
    }
}
