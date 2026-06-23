using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public class InworldEngine : ITtsEngine
{
    private static readonly IReadOnlyList<EngineParameterDef> Schema = new[]
    {
        EngineParameterDef.Slider("speakingRate", "Speed",   0.5, 1.5, 0.05, 1.0),
        EngineParameterDef.Combo("modelId", "Model",
            ["inworld-tts-2", "inworld-tts-1.5-max", "inworld-tts-1.5-mini"],
            "inworld-tts-2"),
        EngineParameterDef.Combo("deliveryMode", "Delivery",
            ["STABLE", "BALANCED", "CREATIVE"],
            "BALANCED"),
    };

    private string _apiKey = string.Empty;
    private readonly HttpClient _http = new() { BaseAddress = new Uri("https://api.inworld.ai") };

    public string EngineId => EngineIds.Inworld;
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

        var speakingRate  = parameters.Dbl("speakingRate", 1.0);
        var modelId       = parameters.Str("modelId",      "inworld-tts-2");
        var deliveryMode  = parameters.Str("deliveryMode", "BALANCED");

        var body = JsonConvert.SerializeObject(new
        {
            text         = text,
            voiceId      = voiceId,
            modelId      = modelId,
            deliveryMode = deliveryMode,
            audioConfig  = new
            {
                encoding        = "MP3",
                sampleRateHertz = 44100,
                speakingRate    = speakingRate,
            },
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/tts/v1/voice")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", _apiKey);

        var response  = await _http.SendAsync(request);
        var respText  = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Inworld TTS failed ({(int)response.StatusCode}): {respText}");

        var b64 = JObject.Parse(respText)["audioContent"]?.Value<string>()
            ?? throw new InvalidOperationException($"Inworld TTS: no audioContent in response: {respText}");

        var mp3Bytes = Convert.FromBase64String(b64);

        using var ms        = new MemoryStream(mp3Bytes);
        using var reader    = new Mp3FileReader(ms);
        using var pcmStream = WaveFormatConversionStream.CreatePcmStream(reader);
        using var pcmMs     = new MemoryStream();
        await pcmStream.CopyToAsync(pcmMs);
        return new AudioData { Samples = pcmMs.ToArray(), Format = pcmStream.WaveFormat };
    }

    public async Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync()
    {
        if (!IsConfigured) return Array.Empty<VoiceInfo>();

        var request = new HttpRequestMessage(HttpMethod.Get, "/tts/v1/voices");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", _apiKey);

        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return Array.Empty<VoiceInfo>();

        var json  = await response.Content.ReadAsStringAsync();
        var arr   = JObject.Parse(json)["voices"] as JArray
                 ?? JArray.Parse(json);

        return arr.Select(v => new VoiceInfo
        {
            Id     = v["voiceId"]?.Value<string>()     ?? string.Empty,
            Name   = v["displayName"]?.Value<string>() ?? string.Empty,
            Locale = (v["languages"] as JArray)?.FirstOrDefault()?.Value<string>() ?? string.Empty,
        })
        .Where(v => !string.IsNullOrEmpty(v.Id))
        .ToList();
    }

    public void Dispose() => _http.Dispose();
}
