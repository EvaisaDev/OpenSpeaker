using System.IO;
using System.Net.Http;
using System.Text;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Infrastructure.Http;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public class TtsMonsterEngine : ITtsEngine
{
    private static readonly HttpClient _wutface  = HttpClientFactory.GetClient("ttsmonster-wutface", "https://wutface.tts.monster/");
    private static readonly HttpClient _gcf      = HttpClientFactory.GetClient("ttsmonster-gcf",     "https://us-central1-tts-monster.cloudfunctions.net/");
    private static readonly HttpClient _official = HttpClientFactory.GetClient("ttsmonster-api",     "https://api.console.tts.monster");
    private static readonly HttpClient _download = HttpClientFactory.GetClient("ttsmonster-dl");

    private string _userId   = string.Empty;
    private string _apiKey   = string.Empty;
    private string _apiToken = string.Empty;

    private bool UseOverlay => !string.IsNullOrEmpty(_userId) && !string.IsNullOrEmpty(_apiKey);

    public string EngineId => EngineIds.TtsMonster;
    public bool IsConfigured => UseOverlay || !string.IsNullOrEmpty(_apiToken);

    public IReadOnlyList<EngineParameterDef> GetParameters() => Array.Empty<EngineParameterDef>();

    public void Configure(string configJson)
    {
        var obj = JObject.Parse(configJson);
        _apiToken = obj["apiToken"]?.Value<string>()?.Trim() ?? string.Empty;

        var overlayUrl = obj["overlayUrl"]?.Value<string>()?.Trim() ?? string.Empty;
        _userId = string.Empty;
        _apiKey = string.Empty;
        if (!string.IsNullOrEmpty(overlayUrl))
            ParseOverlayUrl(overlayUrl);
    }

    private void ParseOverlayUrl(string input)
    {
        var trimmed = input.TrimEnd('/');
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            var parts = uri.AbsolutePath.Trim('/').Split('/');
            if (parts.Length >= 2) { _userId = parts[^2]; _apiKey = parts[^1]; return; }
        }
        var segs = trimmed.Split('/');
        if (segs.Length >= 2) { _userId = segs[^2]; _apiKey = segs[^1]; }
    }

    public async Task<AudioData> SynthesizeAsync(string text, string voiceId, SynthParams parameters)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("TTS.Monster is not configured. Paste your overlay URL or API token in engine settings.");

        return UseOverlay
            ? await SynthesizeOverlay(text, voiceId)
            : await SynthesizeOfficial(text, voiceId);
    }

    private async Task<AudioData> SynthesizeOverlay(string text, string voiceId)
    {
        var payload = new
        {
            data = new
            {
                userId = _userId,
                key    = _apiKey,
                ai     = true,
                message = $"{voiceId}: {text}",
                details = new { provider = "OpenSpeaker" }
            }
        };

        var req = new HttpRequestMessage(HttpMethod.Post, "generateTTS")
        {
            Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
        };
        var resp = await _gcf.SendAsync(req);
        var json = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"generateTTS failed ({(int)resp.StatusCode}): {json}");

        var link = JObject.Parse(json)["data"]?["link"]?.Value<string>() ?? string.Empty;
        if (string.IsNullOrEmpty(link))
            throw new InvalidOperationException($"No audio link in response: {json}");

        return await DownloadWav(link);
    }

    private async Task<AudioData> SynthesizeOfficial(string text, string voiceId)
    {
        var body = new { voice_id = voiceId, message = text, return_usage = false };
        var req = new HttpRequestMessage(HttpMethod.Post, "/generate")
        {
            Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json")
        };
        req.Headers.Add("Authorization", _apiToken);

        var resp = await _official.SendAsync(req);
        var json = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"generate failed ({(int)resp.StatusCode}): {json}");

        var audioUrl = JObject.Parse(json)["url"]?.Value<string>() ?? string.Empty;
        if (string.IsNullOrEmpty(audioUrl))
            throw new InvalidOperationException($"No url in response: {json}");

        return await DownloadWav(audioUrl);
    }

    private async Task<AudioData> DownloadWav(string url)
    {
        var audioBytes = await _download.GetByteArrayAsync(url);
        using var ms        = new MemoryStream(audioBytes);
        using var reader    = new WaveFileReader(ms);
        using var pcmStream = WaveFormatConversionStream.CreatePcmStream(reader);
        using var pcmMs     = new MemoryStream();
        await pcmStream.CopyToAsync(pcmMs);
        return new AudioData { Samples = pcmMs.ToArray(), Format = pcmStream.WaveFormat };
    }

    public async Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync()
    {
        if (!IsConfigured) return Array.Empty<VoiceInfo>();
        return UseOverlay ? await GetVoicesOverlay() : await GetVoicesOfficial();
    }

    private async Task<List<VoiceInfo>> GetVoicesOverlay()
    {
        try
        {
            var voices = new List<VoiceInfo>();

            var body = new { userId = _userId, apiKey = _apiKey };
            var req  = new HttpRequestMessage(HttpMethod.Post, "")
            {
                Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json")
            };
            var resp = await _wutface.SendAsync(req);
            var msg  = JObject.Parse(await resp.Content.ReadAsStringAsync())["message"];

            foreach (var v in msg?["voices"] as JArray ?? [])
            {
                var name = v.Value<string>() ?? string.Empty;
                if (!string.IsNullOrEmpty(name))
                    voices.Add(new VoiceInfo { Id = name, Name = Capitalize(name), Locale = "en-US", Gender = "Neutral" });
            }

            foreach (var v in msg?["customVoices"] as JArray ?? [])
            {
                var name = (v is JObject vo ? vo["name"]?.Value<string>() : v.Value<string>()) ?? string.Empty;
                if (!string.IsNullOrEmpty(name) && !voices.Any(x => x.Id == name))
                    voices.Add(new VoiceInfo { Id = name, Name = $"{Capitalize(name)} (Custom)", Locale = "en-US", Gender = "Neutral" });
            }

            try
            {
                var upResp = await _gcf.GetAsync("getUltraPremiumVoices");
                var upJson = JObject.Parse(await upResp.Content.ReadAsStringAsync());
                foreach (var v in upJson["ultraPremiumVoices"] as JArray ?? [])
                {
                    var name = v.Value<string>() ?? string.Empty;
                    if (!string.IsNullOrEmpty(name) && !voices.Any(x => x.Id == name))
                        voices.Add(new VoiceInfo { Id = name, Name = $"{Capitalize(name)} (Ultra Premium)", Locale = "en-US", Gender = "Neutral" });
                }
            }
            catch { }

            return voices;
        }
        catch { return []; }
    }

    private async Task<List<VoiceInfo>> GetVoicesOfficial()
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/voices");
            req.Headers.Add("Authorization", _apiToken);

            var resp  = await _official.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return [];

            var obj    = JObject.Parse(await resp.Content.ReadAsStringAsync());
            var voices = new List<VoiceInfo>();

            void AddVoices(JToken? arr, bool custom)
            {
                if (arr is not JArray a) return;
                foreach (var v in a)
                {
                    var id   = v["voice_id"]?.Value<string>() ?? string.Empty;
                    var name = v["name"]?.Value<string>() ?? string.Empty;
                    if (string.IsNullOrEmpty(id)) continue;
                    voices.Add(new VoiceInfo
                    {
                        Id     = id,
                        Name   = custom ? $"{name} (Custom)" : name,
                        Locale = v["metadata"]?.Value<string>()?.Split('|').FirstOrDefault() ?? "en-US",
                        Gender = v["metadata"]?.Value<string>()?.Split('|').ElementAtOrDefault(1) ?? "Neutral",
                    });
                }
            }

            AddVoices(obj["voices"],       false);
            AddVoices(obj["customVoices"], true);
            return voices;
        }
        catch { return []; }
    }

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    public void Dispose() { }
}
