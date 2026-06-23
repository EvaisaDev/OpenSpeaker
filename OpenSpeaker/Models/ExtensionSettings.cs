namespace OpenSpeaker.Models;
public class ExtensionSettings
{
    public int Id { get; set; }
    public string ExtensionId { get; set; } = string.Empty;
    public Dictionary<string, string> Values { get; set; } = new();
}
