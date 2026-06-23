namespace OpenSpeaker.Twitch.TwitchEventArgs;
public class SubEventArgs : EventArgs
{
    public string UserId { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Tier { get; init; } = "1000";
    public bool IsGift { get; init; }
    public int CumulativeMonths { get; init; }
}
