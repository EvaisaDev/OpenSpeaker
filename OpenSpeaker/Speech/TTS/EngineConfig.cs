using LiteDB;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS;
public class EngineConfig
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    public string EngineId { get; set; } = EngineIds.Sapi5;
    public bool Enabled { get; set; } = false;
    public string ConfigJson { get; set; } = "{}";
}
