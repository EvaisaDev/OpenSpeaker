using System.Net;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Data;
using OpenSpeaker.Infrastructure.Logging;
using OpenSpeaker.Models;
namespace OpenSpeaker.Twitch;

public record TwitchDeviceCode(string DeviceCode, string UserCode, string VerificationUri, int Interval, DateTime ExpiresAt);

public class TwitchAuthService
{
	public const string AppClientId = "1q4saud1yg0qslb18dsl1uf1r0qpjj";
	public const string BroadcasterScopes = "channel:read:subscriptions channel:read:redemptions bits:read moderator:read:followers moderator:manage:banned_users user:read:chat user:write:chat";
	public const string BotScopes = "user:write:chat user:bot";

	private const int MainAccountId = 1;
	private const int BotAccountId = 2;
	private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(10);
	private static readonly TimeSpan ValidateInterval = TimeSpan.FromHours(1);
	private static readonly TimeSpan MaintenanceInterval = TimeSpan.FromMinutes(1);

	private readonly DatabaseContext _db;
	private readonly IAppLogger? _logger;
	private TwitchAccountInfo? _cached;
	private TwitchAccountInfo? _cachedBot;
	private readonly object _lock = new();
	private readonly SemaphoreSlim _refreshLock = new(1, 1);
	private CancellationTokenSource? _maintenanceCts;
	private DateTime _lastValidated = DateTime.MinValue;

	public event EventHandler? AccountsChanged;

	private static HttpClient Http => OpenSpeaker.Infrastructure.Http.HttpClientFactory.GetClient("twitch-auth");

	public TwitchAuthService(DatabaseContext db, IAppLogger? logger = null)
	{
		_db = db;
		_logger = logger;
	}

	public TwitchAccountInfo? GetAccount()
	{
		lock (_lock)
			return _cached ??= _db.TwitchAccounts.FindById(MainAccountId);
	}

	public void SaveAccount(TwitchAccountInfo info)
	{
		info.Id = MainAccountId;
		_db.TwitchAccounts.Upsert(info);
		lock (_lock) { _cached = info; }
	}

	public void ClearAccount()
	{
		_db.TwitchAccounts.Delete(MainAccountId);
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

	public TwitchAccountInfo? GetBotAccount()
	{
		lock (_lock)
			return _cachedBot ??= _db.TwitchAccounts.FindById(BotAccountId);
	}

	public void SaveBotAccount(TwitchAccountInfo info)
	{
		info.Id = BotAccountId;
		_db.TwitchAccounts.Upsert(info);
		lock (_lock) { _cachedBot = info; }
	}

	public void ClearBotAccount()
	{
		_db.TwitchAccounts.Delete(BotAccountId);
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

	public async Task<TwitchDeviceCode> StartDeviceLoginAsync(bool isBot, CancellationToken ct)
	{
		var (status, body) = await PostFormAsync("https://id.twitch.tv/oauth2/device", new Dictionary<string, string>
		{
			["client_id"] = AppClientId,
			["scopes"] = isBot ? BotScopes : BroadcasterScopes
		}, ct);
		if (status != HttpStatusCode.OK)
			throw new InvalidOperationException($"Twitch refused the login request: {ErrorMessage(body)}");

		return new TwitchDeviceCode(
			(string?)body["device_code"] ?? string.Empty,
			(string?)body["user_code"] ?? string.Empty,
			(string?)body["verification_uri"] ?? "https://www.twitch.tv/activate",
			(int?)body["interval"] ?? 5,
			DateTime.UtcNow.AddSeconds((int?)body["expires_in"] ?? 1800));
	}

	public async Task<TwitchAccountInfo> CompleteDeviceLoginAsync(TwitchDeviceCode code, bool isBot, CancellationToken ct)
	{
		var interval = Math.Max(code.Interval, 1);
		while (DateTime.UtcNow < code.ExpiresAt)
		{
			await Task.Delay(TimeSpan.FromSeconds(interval), ct);
			var (status, body) = await PostFormAsync("https://id.twitch.tv/oauth2/token", new Dictionary<string, string>
			{
				["client_id"] = AppClientId,
				["scopes"] = isBot ? BotScopes : BroadcasterScopes,
				["device_code"] = code.DeviceCode,
				["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
			}, ct);

			if (status == HttpStatusCode.OK)
			{
				var account = new TwitchAccountInfo { ClientId = AppClientId };
				ApplyTokenResponse(account, body);
				await FillUserInfoAsync(account, ct);
				if (isBot) SaveBotAccount(account);
				else SaveAccount(account);
				_logger?.Info($"TWITCH :: Logged in as {account.Login}{(isBot ? " (bot)" : string.Empty)}");
				return account;
			}

			var message = ErrorMessage(body);
			if (message == "authorization_pending") continue;
			if (message == "slow_down") { interval += 5; continue; }
			throw new InvalidOperationException($"Login failed: {message}");
		}
		throw new TimeoutException("The login code expired before it was approved. Please try again.");
	}

	public async Task EnsureFreshAsync(CancellationToken ct = default)
	{
		await RefreshAsync(MainAccountId, false, ct);
		await RefreshAsync(BotAccountId, false, ct);
	}

	public async Task ValidateAsync(CancellationToken ct = default)
	{
		await ValidateAccountAsync(MainAccountId, ct);
		await ValidateAccountAsync(BotAccountId, ct);
	}

	public void StartMaintenance()
	{
		if (_maintenanceCts != null) return;
		_maintenanceCts = new CancellationTokenSource();
		_ = MaintenanceLoopAsync(_maintenanceCts.Token);
	}

	public void StopMaintenance()
	{
		_maintenanceCts?.Cancel();
		_maintenanceCts = null;
	}

	private async Task MaintenanceLoopAsync(CancellationToken ct)
	{
		while (!ct.IsCancellationRequested)
		{
			try
			{
				await EnsureFreshAsync(ct);
				if (DateTime.UtcNow - _lastValidated >= ValidateInterval)
				{
					_lastValidated = DateTime.UtcNow;
					await ValidateAsync(ct);
				}
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
			catch (Exception ex) { _logger?.Warn($"TWITCH :: Token maintenance failed: {ex.Message}"); }

			try { await Task.Delay(MaintenanceInterval, ct); }
			catch (OperationCanceledException) { return; }
		}
	}

	private TwitchAccountInfo? LoadAccount(int id) => id == BotAccountId ? GetBotAccount() : GetAccount();

	private void StoreAccount(int id, TwitchAccountInfo account)
	{
		if (id == BotAccountId) SaveBotAccount(account);
		else SaveAccount(account);
	}

	private static bool CanRefresh(TwitchAccountInfo account) =>
		!string.IsNullOrEmpty(account.AccessToken)
		&& !string.IsNullOrEmpty(account.RefreshToken)
		&& account.ClientId == AppClientId;

	private async Task RefreshAsync(int id, bool force, CancellationToken ct)
	{
		var before = LoadAccount(id);
		if (before == null || !CanRefresh(before)) return;
		if (!force && before.ExpiresAt > DateTime.UtcNow + RefreshMargin) return;
		var staleToken = before.AccessToken;

		await _refreshLock.WaitAsync(ct);
		try
		{
			var account = LoadAccount(id);
			if (account == null || !CanRefresh(account)) return;
			if (force ? account.AccessToken != staleToken : account.ExpiresAt > DateTime.UtcNow + RefreshMargin) return;

			var (status, body) = await PostFormAsync("https://id.twitch.tv/oauth2/token", new Dictionary<string, string>
			{
				["client_id"] = AppClientId,
				["grant_type"] = "refresh_token",
				["refresh_token"] = account.RefreshToken
			}, ct);

			if (status == HttpStatusCode.OK)
			{
				ApplyTokenResponse(account, body);
				StoreAccount(id, account);
				_logger?.Info($"TWITCH :: Refreshed access token for {account.Login}");
			}
			else if (status is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
				MarkExpired(id, account, ErrorMessage(body));
			else
				_logger?.Warn($"TWITCH :: Token refresh for {account.Login} failed ({(int)status}): {ErrorMessage(body)}");
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
		catch (Exception ex) { _logger?.Warn($"TWITCH :: Token refresh failed: {ex.Message}"); }
		finally { _refreshLock.Release(); }
	}

	private async Task ValidateAccountAsync(int id, CancellationToken ct)
	{
		var account = LoadAccount(id);
		if (account == null || string.IsNullOrEmpty(account.AccessToken)) return;

		try
		{
			var req = new HttpRequestMessage(HttpMethod.Get, "https://id.twitch.tv/oauth2/validate");
			req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", account.AccessToken);
			var resp = await Http.SendAsync(req, ct);

			if (resp.StatusCode == HttpStatusCode.Unauthorized)
			{
				if (CanRefresh(account)) await RefreshAsync(id, true, ct);
				else MarkExpired(id, account, "token is no longer valid");
				return;
			}
			if (!resp.IsSuccessStatusCode) return;

			var body = ParseObject(await resp.Content.ReadAsStringAsync(ct));
			var expiresIn = (int?)body["expires_in"];
			if (expiresIn is > 0)
			{
				account.ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn.Value);
				StoreAccount(id, account);
			}
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
		catch (Exception ex) { _logger?.Warn($"TWITCH :: Token validation failed: {ex.Message}"); }
	}

	private void MarkExpired(int id, TwitchAccountInfo account, string reason)
	{
		account.AccessToken = string.Empty;
		account.RefreshToken = string.Empty;
		StoreAccount(id, account);
		_logger?.Warn($"TWITCH :: Login for {account.Login} expired ({reason}). Log in again in the Accounts tab.");
		AccountsChanged?.Invoke(this, EventArgs.Empty);
	}

	private static void ApplyTokenResponse(TwitchAccountInfo account, JObject body)
	{
		account.AccessToken = (string?)body["access_token"] ?? string.Empty;
		var refreshToken = (string?)body["refresh_token"];
		if (!string.IsNullOrEmpty(refreshToken)) account.RefreshToken = refreshToken;
		account.ExpiresAt = DateTime.UtcNow.AddSeconds((int?)body["expires_in"] ?? 0);
		if (body["scope"] is JArray scopes)
			account.Scopes = scopes.Select(s => (string?)s ?? string.Empty).Where(s => s.Length > 0).ToList();
	}

	private static async Task FillUserInfoAsync(TwitchAccountInfo account, CancellationToken ct)
	{
		var req = new HttpRequestMessage(HttpMethod.Get, "https://api.twitch.tv/helix/users");
		req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", account.AccessToken);
		req.Headers.Add("Client-Id", account.ClientId);
		var resp = await Http.SendAsync(req, ct);
		var body = ParseObject(await resp.Content.ReadAsStringAsync(ct));
		var user = body["data"]?.FirstOrDefault()
			?? throw new InvalidOperationException($"Could not fetch your Twitch user info: {ErrorMessage(body)}");

		account.UserId = (string?)user["id"] ?? string.Empty;
		account.Login = (string?)user["login"] ?? string.Empty;
		account.DisplayName = (string?)user["display_name"] ?? string.Empty;
		account.BroadcasterType = (string?)user["broadcaster_type"] ?? string.Empty;
		account.ProfileImageUrl = (string?)user["profile_image_url"] ?? string.Empty;
	}

	private static async Task<(HttpStatusCode Status, JObject Body)> PostFormAsync(string url, Dictionary<string, string> form, CancellationToken ct)
	{
		var resp = await Http.PostAsync(url, new FormUrlEncodedContent(form), ct);
		return (resp.StatusCode, ParseObject(await resp.Content.ReadAsStringAsync(ct)));
	}

	private static JObject ParseObject(string text)
	{
		try { return JObject.Parse(text); }
		catch { return new JObject(); }
	}

	private static string ErrorMessage(JObject body) =>
		(string?)body["message"] ?? (string?)body["error"] ?? "unknown error";
}
