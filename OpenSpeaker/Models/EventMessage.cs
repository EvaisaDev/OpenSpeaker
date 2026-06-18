namespace OpenSpeaker.Models;
public class EventMessage
{
    public string Template { get; set; } = string.Empty;
    public int Weight { get; set; } = 1;
    public bool Enabled { get; set; } = true;
}
