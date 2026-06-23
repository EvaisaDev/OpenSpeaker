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

    private async Task FetchTwitchEmotesAsync(string broadcasterId)
    {
        try
        {
            var token = _auth.GetAccessToken();
            var clientId = _auth.GetClientId();
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(clientId)) return;

            var emotes = new List<string>();

            var globalReq = new HttpRequestMessage(HttpMethod.Get, "https://api.twitch.tv/helix/chat/emotes/global");
            globalReq.Headers.Add("Authorization", $"Bearer {token}");
            globalReq.Headers.Add("Client-Id", clientId);
            var globalResp = await _http.SendAsync(globalReq);
            if (globalResp.IsSuccessStatusCode)
            {
                var json = await globalResp.Content.ReadAsStringAsync();
                var obj = JObject.Parse(json);
                emotes.AddRange(obj["data"]?.Select(e => e["name"]?.ToString() ?? "").Where(s => s.Length > 0) ?? Enumerable.Empty<string>());
            }

            var channelReq = new HttpRequestMessage(HttpMethod.Get, $"https://api.twitch.tv/helix/chat/emotes?broadcaster_id={broadcasterId}");
            channelReq.Headers.Add("Authorization", $"Bearer {token}");
            channelReq.Headers.Add("Client-Id", clientId);
            var channelResp = await _http.SendAsync(channelReq);
            if (channelResp.IsSuccessStatusCode)
            {
                var json = await channelResp.Content.ReadAsStringAsync();
                var obj = JObject.Parse(json);
                emotes.AddRange(obj["data"]?.Select(e => e["name"]?.ToString() ?? "").Where(s => s.Length > 0) ?? Enumerable.Empty<string>());
            }

            _emoteStripper.SetTwitchEmotes(emotes);
            _logger?.Info($"TWITCH :: Loaded {emotes.Count} Twitch emotes");
        }
        catch { }
    }

    private async Task FetchBttvEmotesAsync(string broadcasterId)
    {
        try
        {
            var emotes = new List<string>();

            var globalResp = await _http.GetAsync("https://api.betterttv.net/3/cached/emotes/global");
            if (globalResp.IsSuccessStatusCode)
            {
                var json = await globalResp.Content.ReadAsStringAsync();
                var arr = JArray.Parse(json);
                emotes.AddRange(arr.Select(e => e["code"]?.ToString() ?? "").Where(s => s.Length > 0));
            }

            var channelResp = await _http.GetAsync($"https://api.betterttv.net/3/cached/users/twitch/{broadcasterId}");
            if (channelResp.IsSuccessStatusCode)
            {
                var json = await channelResp.Content.ReadAsStringAsync();
                var obj = JObject.Parse(json);
                emotes.AddRange(obj["channelEmotes"]?.Select(e => e["code"]?.ToString() ?? "").Where(s => s.Length > 0) ?? Enumerable.Empty<string>());
                emotes.AddRange(obj["sharedEmotes"]?.Select(e => e["code"]?.ToString() ?? "").Where(s => s.Length > 0) ?? Enumerable.Empty<string>());
            }

            _emoteStripper.SetBttvEmotes(emotes);
            _logger?.Info($"TWITCH :: Loaded {emotes.Count} BTTV emotes");
        }
        catch { }
    }

    private async Task FetchFfzEmotesAsync(string broadcasterId)
    {
        try
        {
            var emotes = new List<string>();

            var globalResp = await _http.GetAsync("https://api.frankerfacez.com/v1/set/global");
            if (globalResp.IsSuccessStatusCode)
            {
                var json = await globalResp.Content.ReadAsStringAsync();
                var obj = JObject.Parse(json);
                var defaultSets = obj["default_sets"]?.Select(s => s.ToString()).ToHashSet() ?? new HashSet<string>();
                if (obj["sets"] is JObject sets)
                    foreach (var set in sets.Properties())
                    {
                        if (!defaultSets.Contains(set.Name)) continue;
                        if (set.Value["emoticons"] is JArray emoticons)
                            emotes.AddRange(emoticons.Select(e => e["name"]?.ToString() ?? "").Where(s => s.Length > 0));
                    }
            }

            var channelResp = await _http.GetAsync($"https://api.frankerfacez.com/v1/room/id/{broadcasterId}");
            if (channelResp.IsSuccessStatusCode)
            {
                var json = await channelResp.Content.ReadAsStringAsync();
                var obj = JObject.Parse(json);
                if (obj["sets"] is JObject sets)
                    foreach (var set in sets.Properties())
                        if (set.Value["emoticons"] is JArray emoticons)
                            emotes.AddRange(emoticons.Select(e => e["name"]?.ToString() ?? "").Where(s => s.Length > 0));
            }

            _emoteStripper.SetFfzEmotes(emotes);
            _logger?.Info($"TWITCH :: Loaded {emotes.Count} FFZ emotes");
        }
        catch { }
    }

    private async Task FetchSevenTvEmotesAsync(string broadcasterId)
    {
        try
        {
            var emotes = new List<string>();

            var globalResp = await _http.GetAsync("https://7tv.io/v3/emote-sets/global");
            if (globalResp.IsSuccessStatusCode)
            {
                var json = await globalResp.Content.ReadAsStringAsync();
                var obj = JObject.Parse(json);
                emotes.AddRange(obj["emotes"]?.Select(e => e["name"]?.ToString() ?? "").Where(s => s.Length > 0) ?? Enumerable.Empty<string>());
            }

            var channelResp = await _http.GetAsync($"https://7tv.io/v3/users/twitch/{broadcasterId}");
            if (channelResp.IsSuccessStatusCode)
            {
                var json = await channelResp.Content.ReadAsStringAsync();
                var obj = JObject.Parse(json);
                emotes.AddRange(obj["emote_set"]?["emotes"]?.Select(e => e["name"]?.ToString() ?? "").Where(s => s.Length > 0) ?? Enumerable.Empty<string>());
            }

            _emoteStripper.SetSevenTvEmotes(emotes);
            _logger?.Info($"TWITCH :: Loaded {emotes.Count} 7TV emotes");
        }
        catch { }
    }
}
