namespace OpenSpeaker.Twitch.TwitchEventArgs;

public class MessageDeletedEventArgs : EventArgs
{
    public string UserId { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string MessageId { get; init; } = string.Empty;
}

public class UserBannedEventArgs : EventArgs
{
    public string UserId { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public bool IsPermanent { get; init; }
}
