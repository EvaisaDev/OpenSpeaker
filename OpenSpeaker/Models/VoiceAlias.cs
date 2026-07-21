using LiteDB;
namespace OpenSpeaker.Models;
public class VoiceAlias
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    public string Name { get; set; } = string.Empty;
    public string EngineId { get; set; } = EngineIds.Sapi5;
    public string VoiceId { get; set; } = string.Empty;
    public int Volume { get; set; } = 100;
    public string EngineParamsJson { get; set; } = "{}";
    public string OutputDeviceId { get; set; } = string.Empty;
    public List<string> VoiceIds { get; set; } = new();
    public bool LowercaseText { get; set; } = false;
}
