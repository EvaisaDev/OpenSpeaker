using LiteDB;
namespace OpenSpeaker.Models;
public class IgnoreProfile
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    public string Name { get; set; } = string.Empty;
    public List<string> ExcludedVoiceIds { get; set; } = new();
    public List<string> ExcludedLocales { get; set; } = new();
    public bool IsActive { get; set; } = false;
}
