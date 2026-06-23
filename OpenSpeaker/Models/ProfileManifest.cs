namespace OpenSpeaker.Models;
public class ProfileManifest
{
    public string ActiveProfile { get; set; } = "Default";
    public List<string> Profiles { get; set; } = new() { "Default" };
}
