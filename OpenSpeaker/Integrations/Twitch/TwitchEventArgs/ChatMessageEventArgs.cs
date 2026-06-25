namespace OpenSpeaker.Twitch.TwitchEventArgs;
public class ChatMessageEventArgs : EventArgs
{
    public string UserId { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public bool IsCheer { get; init; }
    public int Bits { get; init; }
    public List<string> Roles { get; init; } = new();
    public bool IsSubscriber { get; init; }
    public string Color { get; init; } = string.Empty;
    public bool IsHighlight { get; init; }
    public bool IsReply { get; init; }
    public IReadOnlyList<string> MessageEmotes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> MessageCheermotes { get; init; } = Array.Empty<string>();
}
