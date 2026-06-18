using LiteDB;
namespace OpenSpeaker.Models;
public class TwitchAccountInfo
{
    [BsonId]
    public int Id { get; set; } = 1;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = new();
    public string DisplayName { get; set; } = string.Empty;
    public string BroadcasterType { get; set; } = string.Empty;
    public string ProfileImageUrl { get; set; } = string.Empty;
}
