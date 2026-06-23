using LiteDB;
namespace OpenSpeaker.Models;
public class UserRecord
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    public string TwitchId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string AliasName { get; set; } = string.Empty;
    public string StickyVoiceId { get; set; } = string.Empty;
    public string StickyVoiceEngineId { get; set; } = string.Empty;
    public bool IsIgnored { get; set; } = false;
    public bool IsForced { get; set; } = false;
    public bool IsRegular { get; set; } = false;
    public bool IsSubscribed { get; set; } = false;
    public DateTime LastActive { get; set; } = DateTime.MinValue;
    public string Role { get; set; } = "Viewer";
    public List<PastVoiceEntry> PastVoices { get; set; } = new();
    public override string ToString() => Username;
}

public class PastVoiceEntry
{
    public string VoiceId { get; set; } = string.Empty;
    public string EngineId { get; set; } = string.Empty;
    public DateTime LastUsed { get; set; }
}
