namespace OpenSpeaker.Twitch.TwitchEventArgs;
public class RaidEventArgs : EventArgs
{
    public string FromUserId { get; init; } = string.Empty;
    public string FromUsername { get; init; } = string.Empty;
    public int ViewerCount { get; init; }
}
