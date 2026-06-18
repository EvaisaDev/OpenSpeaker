using LiteDB;
namespace OpenSpeaker.Models;
public class EventConfig
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    public string EventType { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string VoiceAliasOverride { get; set; } = string.Empty;
    public List<EventMessage> Messages { get; set; } = new();
    public EventState State { get; set; } = new();
}
