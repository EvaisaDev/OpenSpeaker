namespace OpenSpeaker.Models;
public class EventState
{
    public bool Enabled { get; set; } = true;
    public int MinRaidViewers { get; set; } = 0;
    public int MinBits { get; set; } = 0;
}
