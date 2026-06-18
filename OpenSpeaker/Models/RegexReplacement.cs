using LiteDB;
namespace OpenSpeaker.Models;
public class RegexReplacement
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    public string Pattern { get; set; } = string.Empty;
    public string Replacement { get; set; } = string.Empty;
    public string Mode { get; set; } = "Replace";
    public bool IsRegex { get; set; } = true;
    public int Order { get; set; } = 0;
    public bool Enabled { get; set; } = true;
}
