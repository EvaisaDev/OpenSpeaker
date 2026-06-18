using OpenSpeaker.Models;
namespace OpenSpeaker.Users;

public class PermissionChecker
{
    public bool CanSpeak(UserRecord user, List<string> roles, AppSettings settings)
    {
        if (user.IsIgnored) return false;
        if (user.IsForced) return true;
        if (roles.Contains(UserRoles.Broadcaster)) return true;
        if (roles.Contains(UserRoles.Moderator) && settings.AllowModerators) return true;
        if ((roles.Contains(UserRoles.Subscriber) || user.IsSubscribed) && settings.AllowSubscribers) return true;
        if (roles.Contains(UserRoles.VIP) && settings.AllowVIPs) return true;
        if (user.IsRegular && settings.AllowRegulars) return true;
        if (settings.AllowEveryone) return true;
        return false;
    }

    public List<string> DetermineRoles(bool isBroadcaster, bool isModerator, bool isSubscriber, bool isVip)
    {
        var roles = new List<string> { UserRoles.Everyone };
        if (isBroadcaster) roles.Add(UserRoles.Broadcaster);
        if (isModerator) roles.Add(UserRoles.Moderator);
        if (isSubscriber) roles.Add(UserRoles.Subscriber);
        if (isVip) roles.Add(UserRoles.VIP);
        return roles;
    }
}
