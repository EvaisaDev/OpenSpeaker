namespace OpenSpeaker.Twitch.TwitchEventArgs;
public class GiftSubEventArgs : EventArgs
{
    public string GiverId { get; init; } = string.Empty;
    public string GiverUsername { get; init; } = string.Empty;
    public string RecipientId { get; init; } = string.Empty;
    public string RecipientUsername { get; init; } = string.Empty;
    public string Tier { get; init; } = "1000";
}
