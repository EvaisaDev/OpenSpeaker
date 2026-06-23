namespace OpenSpeaker.Text;

public class PrefixChecker
{
    public bool StartsWithIgnoredPrefix(string message, IEnumerable<string> ignoredPrefixes)
    {
        foreach (var prefix in ignoredPrefixes)
        {
            if (!string.IsNullOrEmpty(prefix) && message.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
