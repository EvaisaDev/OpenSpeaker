namespace OpenSpeaker.Text;

public static class MentionStripper
{
    public static string StripLeadingMention(string message)
    {
        if (string.IsNullOrEmpty(message) || message[0] != '@') return message;
        var space = message.IndexOf(' ');
        return space < 0 ? string.Empty : message[(space + 1)..].TrimStart();
    }
}
