using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public class CartesiaEngine : ITtsEngine
{
    private static readonly IReadOnlyList<EngineParameterDef> Schema = new[]
    {
        EngineParameterDef.Slider("speed",  "Speed",  -1.0, 1.0, 0.05, 0.0),
        EngineParameterDef.Combo("model", "Model",
            ["sonic-3.5", "sonic-3", "sonic-latest"],
            "sonic-3.5"),
    };

    private string _apiKey = string.Empty;

    private readonly HttpClient _http = new() { BaseAddress = new Uri("https://api.cartesia.ai") };

    public string EngineId => EngineIds.Cartesia;
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

        var speed   = parameters.Dbl("speed", 0.0);
        var modelId = parameters.Str("model", "sonic-3.5");

        var body = JsonConvert.SerializeObject(new
        {
            model_id   = modelId,
            transcript = text,
            voice      = new { mode = "id", id = voiceId },
            output_format = new
            {
                container   = "mp3",
                bit_rate    = 128000,
                sample_rate = 44100,
            },
            generation_config = new { speed },
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/tts/bytes")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("X-API-Key", _apiKey);
        request.Headers.TryAddWithoutValidation("Cartesia-Version", "2026-03-01");

        var response = await _http.SendAsync(request);
        var bytes    = await response.Content.ReadAsByteArrayAsync();

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Cartesia TTS failed ({(int)response.StatusCode}): {Encoding.UTF8.GetString(bytes)}");

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
        string? cursor = null;

        while (true)
        {
            var url = cursor == null ? "/voices?limit=100" : $"/voices?limit=100&starting_after={cursor}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("X-API-Key", _apiKey);
            request.Headers.TryAddWithoutValidation("Cartesia-Version", "2026-03-01");

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) break;

            var json    = await response.Content.ReadAsStringAsync();
            var obj     = JObject.Parse(json);
            var items   = obj["data"] as JArray ?? new JArray();
            var hasMore = obj["has_more"]?.Value<bool>() ?? false;

            foreach (var v in items)
            {
                var id = v["id"]?.Value<string>() ?? string.Empty;
                if (string.IsNullOrEmpty(id)) continue;
                voices.Add(new VoiceInfo
                {
                    Id     = id,
                    Name   = v["name"]?.Value<string>()     ?? id,
                    Locale = v["language"]?.Value<string>() ?? string.Empty,
                    Gender = v["gender"]?.Value<string>()   ?? string.Empty,
                });
                cursor = id;
            }

            if (!hasMore || items.Count == 0) break;
        }

        return voices;
    }

    public void Dispose() => _http.Dispose();
}
