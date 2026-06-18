using System.IO;
using System.Net.Http;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Infrastructure.Http;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public class TtsMonsterEngine : ITtsEngine
{
    private string _userId = string.Empty;
    private string _apiKey = string.Empty;
    private string _apiToken = string.Empty;

    private readonly HttpClient _overlayHttp  = HttpClientFactory.GetClient("ttsmonster_overlay",  "https://api.tts.monster");
    private readonly HttpClient _consoleHttp  = HttpClientFactory.GetClient("ttsmonster_console", "https://api.console.tts.monster");

    public string EngineId => EngineIds.TtsMonster;

    private bool HasConsoleToken => !string.IsNullOrEmpty(_apiToken);
    private bool HasOverlay => !string.IsNullOrEmpty(_userId) && !string.IsNullOrEmpty(_apiKey);
    public bool IsConfigured => HasConsoleToken || HasOverlay;

    public IReadOnlyList<EngineParameterDef> GetParameters() => Array.Empty<EngineParameterDef>();

    public void Configure(string configJson)
    {
        var obj = JObject.Parse(configJson);
        _apiToken = obj["apiToken"]?.Value<string>() ?? string.Empty;

        var overlayUrl = obj["overlayUrl"]?.Value<string>() ?? string.Empty;
        if (!string.IsNullOrEmpty(overlayUrl))
        {
            try
            {
                var uri = new Uri(overlayUrl);
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                var qUserId = query["user_id"];
                var qApiKey = query["api_key"];

                if (!string.IsNullOrEmpty(qUserId) && !string.IsNullOrEmpty(qApiKey))
                {
                    _userId = qUserId;
                    _apiKey = qApiKey;
                }
                else
                {
                    var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length >= 2)
                    {
                        _userId = segments[^2];
                        _apiKey = segments[^1];
                    }
                }
            }
            catch { }
        }
        else
        {
            _userId = obj["userId"]?.Value<string>() ?? string.Empty;
            _apiKey = obj["apiKey"]?.Value<string>() ?? string.Empty;
        }
    }

    public async Task<AudioData> SynthesizeAsync(string text, string voiceId, SynthParams parameters)
    {
        if (!IsConfigured) return AudioData.Empty;

        try
        {
            string audioUrl;

            if (HasConsoleToken)
            {
                var body = new { voice_id = voiceId, message = text, return_usage = false };
                var request = new HttpRequestMessage(HttpMethod.Post, "/generate")
                {
                    Content = new StringContent(JsonConvert.SerializeObject(body), System.Text.Encoding.UTF8, "application/json")
                };
                request.Headers.Add("Authorization", _apiToken);

                var response = await _consoleHttp.SendAsync(request);
                if (!response.IsSuccessStatusCode) return AudioData.Empty;

                var json = await response.Content.ReadAsStringAsync();
                audioUrl = JObject.Parse(json)["url"]?.Value<string>() ?? string.Empty;
            }
            else
            {
                var body = new { message = text, voice = voiceId, user_id = _userId, api_key = _apiKey };
                var content = new StringContent(JsonConvert.SerializeObject(body), System.Text.Encoding.UTF8, "application/json");
                var response = await _overlayHttp.PostAsync("/generate", content);
                if (!response.IsSuccessStatusCode) return AudioData.Empty;

                var json = await response.Content.ReadAsStringAsync();
                audioUrl = JObject.Parse(json)["audio"]?.Value<string>() ?? string.Empty;
            }

            if (string.IsNullOrEmpty(audioUrl)) return AudioData.Empty;

            using var dlClient = new HttpClient();
            var audioBytes = await dlClient.GetByteArrayAsync(audioUrl);
            using var ms = new MemoryStream(audioBytes);
            using var reader = new Mp3FileReader(ms);
            using var pcmStream = WaveFormatConversionStream.CreatePcmStream(reader);
            using var pcmMs = new MemoryStream();
            await pcmStream.CopyToAsync(pcmMs);
            return new AudioData { Samples = pcmMs.ToArray(), Format = pcmStream.WaveFormat };
        }
        catch
        {
            return AudioData.Empty;
        }
    }

    public async Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync()
    {
        if (!HasConsoleToken) return Array.Empty<VoiceInfo>();

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/voices");
            request.Headers.Add("Authorization", _apiToken);

            var response = await _consoleHttp.SendAsync(request);
            if (!response.IsSuccessStatusCode) return Array.Empty<VoiceInfo>();

            var json = await response.Content.ReadAsStringAsync();
            var obj = JObject.Parse(json);

            var voices = new List<VoiceInfo>();
            void AddVoices(JToken? arr, bool custom)
            {
                if (arr is not JArray a) return;
                foreach (var v in a)
                {
                    var id = v["voice_id"]?.Value<string>() ?? string.Empty;
                    var name = v["name"]?.Value<string>() ?? string.Empty;
                    if (string.IsNullOrEmpty(id)) continue;
                    voices.Add(new VoiceInfo
                    {
                        Id = id,
                        Name = custom ? $"{name} (Custom)" : name,
                        Locale = v["metadata"]?.Value<string>()?.Split('|').FirstOrDefault() ?? "en-US",
                        Gender = v["metadata"]?.Value<string>()?.Split('|').ElementAtOrDefault(1) ?? "Neutral",
                    });
                }
            }

            AddVoices(obj["voices"], false);
            AddVoices(obj["customVoices"], true);

            return voices;
        }
        catch
        {
            return Array.Empty<VoiceInfo>();
        }
    }

    public void Dispose() { }
}
