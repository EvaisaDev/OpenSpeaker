using LiteDB;
namespace OpenSpeaker.Models;
public class CustomCommand
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    public string Trigger { get; set; } = string.Empty;
    public string VoiceAliasName { get; set; } = string.Empty;
    public List<string> AllowedRoles { get; set; } = new() { UserRoles.Everyone };
    public bool Enabled { get; set; } = true;
}
