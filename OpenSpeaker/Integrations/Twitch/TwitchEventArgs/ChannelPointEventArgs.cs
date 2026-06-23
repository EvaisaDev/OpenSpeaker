namespace OpenSpeaker.Twitch.TwitchEventArgs;
public class ChannelPointEventArgs : EventArgs
{
    public string UserId { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string RewardId { get; init; } = string.Empty;
    public string RewardTitle { get; init; } = string.Empty;
    public int RewardCost { get; init; }
    public string Input { get; init; } = string.Empty;
}
