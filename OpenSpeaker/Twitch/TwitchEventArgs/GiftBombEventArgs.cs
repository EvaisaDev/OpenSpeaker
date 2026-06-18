namespace OpenSpeaker.Twitch.TwitchEventArgs;
public class GiftBombEventArgs : EventArgs
{
    public string GiverId { get; init; } = string.Empty;
    public string GiverUsername { get; init; } = string.Empty;
    public int GiftCount { get; init; }
    public string Tier { get; init; } = "1000";
}
