using OpenSpeaker.Data;
using OpenSpeaker.Models;
namespace OpenSpeaker.Twitch;

public class TwitchAuthService
{
    private readonly DatabaseContext _db;
    private TwitchAccountInfo? _cached;
    private readonly object _lock = new();

    public TwitchAuthService(DatabaseContext db)
    {
        _db = db;
    }

    public TwitchAccountInfo? GetAccount()
    {
        lock (_lock)
            return _cached ??= _db.TwitchAccounts.FindById(1);
    }

    public void SaveAccount(TwitchAccountInfo info)
    {
        info.Id = 1;
        _db.TwitchAccounts.Upsert(info);
        lock (_lock) { _cached = info; }
    }

    public void ClearAccount()
    {
        _db.TwitchAccounts.Delete(1);
        lock (_lock) { _cached = null; }
    }

    public bool HasValidAccount()
    {
        var account = GetAccount();
        return account != null && !string.IsNullOrEmpty(account.AccessToken);
    }

    public string? GetAccessToken() => GetAccount()?.AccessToken;
    public string? GetUserId() => GetAccount()?.UserId;
    public string? GetLogin() => GetAccount()?.Login;
    public string? GetDisplayName() => GetAccount()?.DisplayName;
    public string? GetBroadcasterType() => GetAccount()?.BroadcasterType;
    public string? GetClientId() => GetAccount()?.ClientId;
    public string? GetProfileImageUrl() => GetAccount()?.ProfileImageUrl;

    private TwitchAccountInfo? _cachedBot;

    public TwitchAccountInfo? GetBotAccount()
    {
        lock (_lock)
            return _cachedBot ??= _db.TwitchAccounts.FindById(2);
    }

    public void SaveBotAccount(TwitchAccountInfo info)
    {
        info.Id = 2;
        _db.TwitchAccounts.Upsert(info);
        lock (_lock) { _cachedBot = info; }
    }

    public void ClearBotAccount()
    {
        _db.TwitchAccounts.Delete(2);
        lock (_lock) { _cachedBot = null; }
    }

    public bool HasBotAccount()
    {
        var account = GetBotAccount();
        return account != null && !string.IsNullOrEmpty(account.AccessToken);
    }

    public string? GetBotLogin() => GetBotAccount()?.Login;
    public string? GetBotDisplayName() => GetBotAccount()?.DisplayName;
    public string? GetBotProfileImageUrl() => GetBotAccount()?.ProfileImageUrl;

    public TwitchLib.Api.TwitchAPI CreateApi()
    {
        var account = GetAccount();
        var api = new TwitchLib.Api.TwitchAPI();
        if (account != null)
        {
            api.Settings.ClientId = account.ClientId;
            api.Settings.AccessToken = account.AccessToken;
        }
        return api;
    }
}
