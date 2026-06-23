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

    [BsonIgnore]
    public string PermissionsSummary
    {
        get
        {
            if (AllowedRoles.Contains(UserRoles.Everyone)) return "Everyone";
            var parts = new List<string>();
            if (AllowedRoles.Contains(UserRoles.Moderator))  parts.Add("Mods");
            if (AllowedRoles.Contains(UserRoles.Subscriber)) parts.Add("Subs");
            if (AllowedRoles.Contains(UserRoles.VIP))        parts.Add("VIPs");
            if (AllowedRoles.Contains(UserRoles.Regular))    parts.Add("Regulars");
            return parts.Count > 0 ? string.Join(", ", parts) : "Broadcaster";
        }
    }
}
