using OpenSpeaker.Models;
namespace OpenSpeaker.Users;
public interface IUserService
{
    Task<UserRecord> GetOrCreateAsync(string twitchId, string username);
    Task SetIgnoredAsync(string username, bool ignored);
    Task SetForcedAsync(string username, bool forced);
    Task SetRegularAsync(string username, bool regular);
    Task SetNicknameAsync(string username, string nickname);
    Task SetStickyRandomVoiceAsync(string twitchId, string voiceId, string engineId);
    Task AssignLastVoiceAsync(string username, string voiceId, string engineId);
    Task ResetRandomVoiceAsync(string username);
    Task UpdateSubscribedAsync(string twitchId, bool isSubscribed);
    Task AddPastVoiceAsync(string twitchId, string voiceId, string engineId);
    Task TouchLastActiveAsync(string twitchId, string username);
}
