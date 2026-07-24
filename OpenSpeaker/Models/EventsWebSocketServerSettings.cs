namespace OpenSpeaker.Models;
public class EventsWebSocketServerSettings
{
    public bool AutoStart { get; set; } = false;
    public string Address { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 7581;
}
