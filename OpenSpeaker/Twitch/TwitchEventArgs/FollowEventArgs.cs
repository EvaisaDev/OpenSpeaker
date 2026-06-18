namespace OpenSpeaker.Twitch.TwitchEventArgs;
public class FollowEventArgs : EventArgs
{
    public string UserId { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
}
