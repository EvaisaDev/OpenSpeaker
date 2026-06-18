namespace OpenSpeaker.Twitch.TwitchEventArgs;
public class GoalEventArgs : EventArgs
{
    public string Type { get; init; } = string.Empty;
    public int CurrentAmount { get; init; }
    public int TargetAmount { get; init; }
}
