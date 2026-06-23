using System.Net.Http;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Infrastructure.Logging;
using OpenSpeaker.Text;
namespace OpenSpeaker.Twitch;

public class EmoteCacheService
{
    private readonly TwitchAuthService _auth;
    private readonly EmoteStripper _emoteStripper;
    private readonly IAppLogger? _logger;
    private readonly HttpClient _http = new();

    public EmoteCacheService(TwitchAuthService auth, EmoteStripper emoteStripper, IAppLogger? logger = null)
    {
        _auth = auth;
        _emoteStripper = emoteStripper;
        _logger = logger;
    }

    public async Task RefreshAsync(string broadcasterId)
    {
        await Task.WhenAll(
            FetchTwitchEmotesAsync(broadcasterId),
            FetchBttvEmotesAsync(broadcasterId),
            FetchFfzEmotesAsync(broadcasterId),
            FetchSevenTvEmotesAsync(broadcasterId)
        );
    }

    private async Task<JToken?> GetJsonAsync(string url, string? token = null, string? clientId = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrEmpty(token)) req.Headers.Add("Authorization", $"Bearer {token}");
        if (!string.IsNullOrEmpty(clientId)) req.Headers.Add("Client-Id", clientId);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return null;
        return JToken.Parse(await resp.Content.ReadAsStringAsync());
    }

    private async Task FetchTwitchEmotesAsync(string broadcasterId)
    {
        try
        {
            var token = _auth.GetAccessToken();
            var clientId = _auth.GetClientId();
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(clientId)) return;

            var emotes = new List<string>();

            if (await GetJsonAsync("https://api.twitch.tv/helix/chat/emotes/global", token, clientId) is JObject globalObj)
                emotes.AddRange(globalObj["data"]?.Select(e => e["name"]?.ToString() ?? "").Where(s => s.Length > 0) ?? Enumerable.Empty<string>());

            if (await GetJsonAsync($"https://api.twitch.tv/helix/chat/emotes?broadcaster_id={broadcasterId}", token, clientId) is JObject channelObj)
                emotes.AddRange(channelObj["data"]?.Select(e => e["name"]?.ToString() ?? "").Where(s => s.Length > 0) ?? Enumerable.Empty<string>());

            _emoteStripper.SetTwitchEmotes(emotes);
            _logger?.Info($"TWITCH :: Loaded {emotes.Count} Twitch emotes");
        }
        catch (Exception ex) { _logger?.Warn($"TWITCH :: Failed to load Twitch emotes: {ex.Message}"); }
    }

    private async Task FetchBttvEmotesAsync(string broadcasterId)
    {
        try
        {
            var emotes = new List<string>();

            if (await GetJsonAsync("https://api.betterttv.net/3/cached/emotes/global") is JArray globalArr)
                emotes.AddRange(globalArr.Select(e => e["code"]?.ToString() ?? "").Where(s => s.Length > 0));

            if (await GetJsonAsync($"https://api.betterttv.net/3/cached/users/twitch/{broadcasterId}") is JObject obj)
            {
                emotes.AddRange(obj["channelEmotes"]?.Select(e => e["code"]?.ToString() ?? "").Where(s => s.Length > 0) ?? Enumerable.Empty<string>());
                emotes.AddRange(obj["sharedEmotes"]?.Select(e => e["code"]?.ToString() ?? "").Where(s => s.Length > 0) ?? Enumerable.Empty<string>());
            }

            _emoteStripper.SetBttvEmotes(emotes);
            _logger?.Info($"TWITCH :: Loaded {emotes.Count} BTTV emotes");
        }
        catch (Exception ex) { _logger?.Warn($"TWITCH :: Failed to load BTTV emotes: {ex.Message}"); }
    }

    private async Task FetchFfzEmotesAsync(string broadcasterId)
    {
        try
        {
            var emotes = new List<string>();

            if (await GetJsonAsync("https://api.frankerfacez.com/v1/set/global") is JObject globalObj)
            {
                var defaultSets = globalObj["default_sets"]?.Select(s => s.ToString()).ToHashSet() ?? new HashSet<string>();
                if (globalObj["sets"] is JObject sets)
                    foreach (var set in sets.Properties())
                    {
                        if (!defaultSets.Contains(set.Name)) continue;
                        if (set.Value["emoticons"] is JArray emoticons)
                            emotes.AddRange(emoticons.Select(e => e["name"]?.ToString() ?? "").Where(s => s.Length > 0));
                    }
            }

            if (await GetJsonAsync($"https://api.frankerfacez.com/v1/room/id/{broadcasterId}") is JObject channelObj)
            {
                if (channelObj["sets"] is JObject sets)
                    foreach (var set in sets.Properties())
                        if (set.Value["emoticons"] is JArray emoticons)
                            emotes.AddRange(emoticons.Select(e => e["name"]?.ToString() ?? "").Where(s => s.Length > 0));
            }

            _emoteStripper.SetFfzEmotes(emotes);
            _logger?.Info($"TWITCH :: Loaded {emotes.Count} FFZ emotes");
        }
        catch (Exception ex) { _logger?.Warn($"TWITCH :: Failed to load FFZ emotes: {ex.Message}"); }
    }

    private async Task FetchSevenTvEmotesAsync(string broadcasterId)
    {
        try
        {
            var emotes = new List<string>();

            if (await GetJsonAsync("https://7tv.io/v3/emote-sets/global") is JObject globalObj)
                emotes.AddRange(globalObj["emotes"]?.Select(e => e["name"]?.ToString() ?? "").Where(s => s.Length > 0) ?? Enumerable.Empty<string>());

            if (await GetJsonAsync($"https://7tv.io/v3/users/twitch/{broadcasterId}") is JObject channelObj)
                emotes.AddRange(channelObj["emote_set"]?["emotes"]?.Select(e => e["name"]?.ToString() ?? "").Where(s => s.Length > 0) ?? Enumerable.Empty<string>());

            _emoteStripper.SetSevenTvEmotes(emotes);
            _logger?.Info($"TWITCH :: Loaded {emotes.Count} 7TV emotes");
        }
        catch (Exception ex) { _logger?.Warn($"TWITCH :: Failed to load 7TV emotes: {ex.Message}"); }
    }
}
