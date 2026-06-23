namespace OpenSpeaker.Models;

public class ExtensionConfig
{
    public int Id { get; set; }
    public string ExtensionId { get; set; } = string.Empty;
    public Dictionary<string, string> AuthValues { get; set; } = new();
}
