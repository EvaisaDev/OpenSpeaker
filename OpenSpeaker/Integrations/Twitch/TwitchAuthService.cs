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
