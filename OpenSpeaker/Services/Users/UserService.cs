using OpenSpeaker.Data;
using OpenSpeaker.Models;
namespace OpenSpeaker.Users;

public class UserService : IUserService
{
    private readonly UserRepository _userRepo;
    private readonly VoiceAliasRepository _aliasRepo;
    private readonly object _lock = new();

    public UserService(UserRepository userRepo, VoiceAliasRepository aliasRepo)
    {
        _userRepo = userRepo;
        _aliasRepo = aliasRepo;
    }

    private Task LockedAsync(Action action) => Task.Run(() => { lock (_lock) { action(); } });
    private Task<T> LockedAsync<T>(Func<T> func) => Task.Run(() => { lock (_lock) { return func(); } });

    public Task<UserRecord> GetOrCreateAsync(string twitchId, string username) =>
        LockedAsync(() =>
        {
            var user = _userRepo.FindByTwitchId(twitchId);
            if (user != null) return user;

            user = new UserRecord { TwitchId = twitchId, Username = username };
            _userRepo.Upsert(user);
            return user;
        });

    public Task UpdateSubscribedAsync(string twitchId, bool isSubscribed) =>
        LockedAsync(() =>
        {
            var user = _userRepo.FindByTwitchId(twitchId);
            if (user == null || user.IsSubscribed == isSubscribed) return;
            user.IsSubscribed = isSubscribed;
            _userRepo.Upsert(user);
        });

    public Task TouchLastActiveAsync(string twitchId, string username) =>
        LockedAsync(() =>
        {
            var user = _userRepo.FindByTwitchId(twitchId);
            if (user == null)
            {
                user = new UserRecord { TwitchId = twitchId, Username = username, LastActive = DateTime.Now };
                _userRepo.Upsert(user);
                return;
            }
            user.LastActive = DateTime.Now;
            _userRepo.Upsert(user);
        });

    public Task AddPastVoiceAsync(string twitchId, string voiceId, string engineId)
    {
        if (string.IsNullOrEmpty(voiceId)) return Task.CompletedTask;
        return LockedAsync(() =>
        {
            var user = _userRepo.FindByTwitchId(twitchId);
            if (user == null) return;
            var existing = user.PastVoices.FirstOrDefault(v => v.VoiceId == voiceId && v.EngineId == engineId);
            if (existing != null)
                existing.LastUsed = DateTime.UtcNow;
            else
                user.PastVoices.Add(new PastVoiceEntry { VoiceId = voiceId, EngineId = engineId, LastUsed = DateTime.UtcNow });
            user.PastVoices = user.PastVoices.OrderByDescending(v => v.LastUsed).ToList();
            _userRepo.Upsert(user);
        });
    }

    public async Task SetIgnoredAsync(string username, bool ignored) =>
        await UpdateByUsernameAsync(username, u => u.IsIgnored = ignored);

    public async Task SetForcedAsync(string username, bool forced) =>
        await UpdateByUsernameAsync(username, u => u.IsForced = forced);

    public async Task SetRegularAsync(string username, bool regular) =>
        await UpdateByUsernameAsync(username, u => u.IsRegular = regular);

    public async Task SetNicknameAsync(string username, string nickname) =>
        await UpdateByUsernameAsync(username, u => u.Nickname = nickname);

    public Task SetStickyRandomVoiceAsync(string twitchId, string voiceId, string engineId) =>
        LockedAsync(() =>
        {
            var user = _userRepo.FindByTwitchId(twitchId);
            if (user == null) return;
            user.StickyVoiceId = voiceId;
            user.StickyVoiceEngineId = engineId;
            _userRepo.Upsert(user);
        });

    public async Task AssignLastVoiceAsync(string username, string voiceId, string engineId) =>
        await UpdateByUsernameAsync(username, u =>
        {
            u.StickyVoiceId = voiceId;
            u.StickyVoiceEngineId = engineId;
        });

    public async Task ResetRandomVoiceAsync(string username) =>
        await UpdateByUsernameAsync(username, u =>
        {
            u.StickyVoiceId = string.Empty;
            u.StickyVoiceEngineId = string.Empty;
        });

    private Task UpdateByUsernameAsync(string username, Action<UserRecord> update) =>
        LockedAsync(() =>
        {
            var user = _userRepo.FindByUsername(username);
            if (user == null) return;
            update(user);
            _userRepo.Upsert(user);
        });
}
