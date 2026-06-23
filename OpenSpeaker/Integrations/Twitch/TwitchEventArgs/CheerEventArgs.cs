namespace OpenSpeaker.Twitch.TwitchEventArgs;
public class CheerEventArgs : EventArgs
{
    public string UserId { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public int Bits { get; init; }
    public string Message { get; init; } = string.Empty;
}
