namespace OpenSpeaker.Twitch.TwitchEventArgs;
public class HypeTrainEventArgs : EventArgs
{
    public int Level { get; init; }
    public int Progress { get; init; }
    public int Total { get; init; }
    public int Percent => Total > 0 ? (int)((double)Progress / Total * 100) : 0;
}
