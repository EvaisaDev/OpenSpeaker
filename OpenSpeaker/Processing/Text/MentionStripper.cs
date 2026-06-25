namespace OpenSpeaker.Text;

public static class MentionStripper
{
    /// <summary>
    /// Removes a single leading "@mention " token. Twitch prepends "@parentuser " to the body
    /// of reply messages; this lets prefix/command matching see the actual message content.
    /// </summary>
    public static string StripLeadingMention(string message)
    {
        if (string.IsNullOrEmpty(message) || message[0] != '@') return message;
        var space = message.IndexOf(' ');
        return space < 0 ? string.Empty : message[(space + 1)..].TrimStart();
    }
}
