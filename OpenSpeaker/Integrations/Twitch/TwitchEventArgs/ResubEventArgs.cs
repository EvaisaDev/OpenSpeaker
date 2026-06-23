namespace OpenSpeaker.Twitch.TwitchEventArgs;
public class ResubEventArgs : EventArgs
{
    public string UserId { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Tier { get; init; } = "1000";
    public int CumulativeMonths { get; init; }
    public string Message { get; init; } = string.Empty;
}
