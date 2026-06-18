using LiteDB;
namespace OpenSpeaker.Models;
public class ChannelReward
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    public string TwitchRewardId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Cost { get; set; } = 0;
    public bool IsIgnored { get; set; } = false;
    public bool SayInput { get; set; } = false;
    public string VoiceAliasName { get; set; } = string.Empty;
    public List<EventMessage> Messages { get; set; } = new();
}
