using LiteDB;
namespace OpenSpeaker.Models;
public class BadWordEntry
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    public string Word { get; set; } = string.Empty;
}
