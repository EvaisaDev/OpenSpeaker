using LiteDB;
namespace OpenSpeaker.Models;
public class VoiceGateProfile
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    public string Name { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public float ThresholdDb { get; set; } = -30f;
    public int TimeoutMs { get; set; } = 1000;
    public bool Enabled { get; set; } = false;
    public bool IsActive { get; set; } = false;
}
