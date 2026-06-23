using System.IO;
using System.Net.Http;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSpeaker.ThingsIDKWhereToPut.Http;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public class ElevenLabsEngine : ITtsEngine
{
    private static readonly IReadOnlyList<string> FallbackModelOptions = new[]
    {
        "eleven_multilingual_v2", "eleven_turbo_v2_5", "eleven_flash_v2_5",
        "eleven_v3", "eleven_monolingual_v1"
    };

    private IReadOnlyList<string> _modelOptions = FallbackModelOptions;
    private IReadOnlyList<EngineParameterDef>? _schema;

    private string _apiKey = string.Empty;
    private readonly HttpClient _http = HttpClientFactory.GetClient("elevenlabs", "https://api.elevenlabs.io");

    public string EngineId => EngineIds.ElevenLabs;
    public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);

    public IReadOnlyList<EngineParameterDef> GetParameters()
    {
        return _schema ??= BuildSchema();
    }

    private IReadOnlyList<EngineParameterDef> BuildSchema() => new[]
    {
        EngineParameterDef.Slider("stability", "Stability", 0, 1, 0.01, 0.5),
        EngineParameterDef.Slider("similarity_boost", "Similarity", 0, 1, 0.01, 0.75),
        EngineParameterDef.Slider("style", "Style", 0, 1, 0.01, 0),
        EngineParameterDef.Combo("model", "Model", _modelOptions, _modelOptions[0])
    };

    public void Configure(string configJson)
    {
        var obj = JObject.Parse(configJson);
        _apiKey = obj["apiKey"]?.Value<string>() ?? string.Empty;
        _schema = null;
        if (IsConfigured)
            _ = RefreshModelsAsync();
    }

    private async Task RefreshModelsAsync()
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/v1/models");
            request.Headers.Add("xi-api-key", _apiKey);
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return;
            var json = await response.Content.ReadAsStringAsync();
            var arr = JArray.Parse(json);
            var ids = arr
                .Where(m => m["can_do_text_to_speech"]?.Value<bool>() == true)
                .Select(m => m["model_id"]?.Value<string>() ?? string.Empty)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();
            if (ids.Count > 0)
            {
                _modelOptions = ids;
                _schema = null;
            }
        }
        catch { }
    }

    public async Task<AudioData> SynthesizeAsync(string text, string voiceId, SynthParams parameters)
    {
        if (!IsConfigured) return AudioData.Empty;

        var body = new
        {
            text,
            model_id = parameters.Str("model", "eleven_multilingual_v2"),
            voice_settings = new
            {
                stability = parameters.Dbl("stability", 0.5),
                similarity_boost = parameters.Dbl("similarity_boost", 0.75),
                style = parameters.Dbl("style", 0)
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/text-to-speech/{voiceId}")
        {
            Content = new StringContent(JsonConvert.SerializeObject(body), System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Add("xi-api-key", _apiKey);
        request.Headers.Add("Accept", "audio/mpeg");

        try
        {
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[ElevenLabs] Synthesis failed {(int)response.StatusCode}: {err}");
                throw new Exception($"ElevenLabs synthesis failed ({(int)response.StatusCode}): {err}");
            }

            var mp3Bytes = await response.Content.ReadAsByteArrayAsync();
            using var ms = new MemoryStream(mp3Bytes);
            using var reader = new Mp3FileReader(ms);
            using var pcmStream = WaveFormatConversionStream.CreatePcmStream(reader);
            using var pcmMs = new MemoryStream();
            await pcmStream.CopyToAsync(pcmMs);
            return new AudioData { Samples = pcmMs.ToArray(), Format = pcmStream.WaveFormat };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ElevenLabs] SynthesizeAsync exception: {ex.Message}");
            throw;
        }
    }

    public async Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync()
    {
        if (!IsConfigured) return Array.Empty<VoiceInfo>();

        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/voices");
        request.Headers.Add("xi-api-key", _apiKey);

        try
        {
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return Array.Empty<VoiceInfo>();

            var json = await response.Content.ReadAsStringAsync();
            var obj = JObject.Parse(json);
            return obj["voices"]!.Select(v => new VoiceInfo
            {
                Id = v["voice_id"]!.Value<string>() ?? string.Empty,
                Name = v["name"]!.Value<string>() ?? string.Empty,
                Locale = "en-US",
                Gender = "Neutral"
            }).ToList();
        }
        catch
        {
            return Array.Empty<VoiceInfo>();
        }
    }

    public void Dispose() { }
}
