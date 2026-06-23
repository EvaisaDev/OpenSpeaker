using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public class FishAudioEngine : ITtsEngine
{
    private static readonly IReadOnlyList<EngineParameterDef> Schema = new[]
    {
        EngineParameterDef.Slider("speed", "Speed", 0.5, 2.0, 0.05, 1.0),
        EngineParameterDef.Combo("model", "Model", ["s2-pro", "s1"], "s2-pro"),
    };

    private string _apiKey = string.Empty;
    private readonly HttpClient _http = new() { BaseAddress = new Uri("https://api.fish.audio") };

    public string EngineId => EngineIds.FishAudio;
    public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);
    public IReadOnlyList<EngineParameterDef> GetParameters() => Schema;

    public void Configure(string configJson)
    {
        var obj = JObject.Parse(configJson);
        _apiKey = obj["apiKey"]?.Value<string>() ?? string.Empty;
    }

    public async Task<AudioData> SynthesizeAsync(string text, string voiceId, SynthParams parameters)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(text)) return AudioData.Empty;

        var speed = parameters.Dbl("speed", 1.0);
        var model = parameters.Str("model", "s2-pro");

        var body = JsonConvert.SerializeObject(new
        {
            text         = text,
            reference_id = voiceId,
            format       = "mp3",
            model        = model,
            prosody      = new { speed },
            latency      = "normal",
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/tts")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        var response = await _http.SendAsync(request);
        var bytes    = await response.Content.ReadAsByteArrayAsync();

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Fish Audio TTS failed ({(int)response.StatusCode}): {Encoding.UTF8.GetString(bytes)}");

        using var ms        = new MemoryStream(bytes);
        using var reader    = new Mp3FileReader(ms);
        using var pcmStream = WaveFormatConversionStream.CreatePcmStream(reader);
        using var pcmMs     = new MemoryStream();
        await pcmStream.CopyToAsync(pcmMs);
        return new AudioData { Samples = pcmMs.ToArray(), Format = pcmStream.WaveFormat };
    }

    public async Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync()
    {
        if (!IsConfigured) return Array.Empty<VoiceInfo>();

        var voices = new List<VoiceInfo>();
        var page   = 1;

        while (true)
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"/model?page_size=50&page_number={page}&sort_by=task_count");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) break;

            var json    = await response.Content.ReadAsStringAsync();
            var obj     = JObject.Parse(json);
            var items   = obj["items"] as JArray ?? new JArray();
            var hasMore = obj["has_more"]?.Value<bool>() ?? false;

            foreach (var item in items)
            {
                var id = item["_id"]?.Value<string>() ?? item["id"]?.Value<string>() ?? string.Empty;
                if (string.IsNullOrEmpty(id)) continue;
                var lang = (item["languages"] as JArray)?.FirstOrDefault()?.Value<string>()
                        ?? (item["language"] as JArray)?.FirstOrDefault()?.Value<string>()
                        ?? string.Empty;
                voices.Add(new VoiceInfo
                {
                    Id     = id,
                    Name   = item["title"]?.Value<string>() ?? id,
                    Locale = lang,
                });
            }

            if (!hasMore || items.Count == 0) break;
            page++;
        }

        return voices;
    }

    public void Dispose() => _http.Dispose();
}
