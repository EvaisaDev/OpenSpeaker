namespace OpenSpeaker.Models;
public class VoiceGateProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public float ThresholdDb { get; set; } = -75f;
    public float ResumeThresholdDb { get; set; } = -85f;
    public int TimeoutMs { get; set; } = 5000;
    public bool Enabled { get; set; } = false;
    public bool IsActive { get; set; } = false;
}
