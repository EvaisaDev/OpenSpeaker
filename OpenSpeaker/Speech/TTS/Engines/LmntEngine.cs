using System.IO;
using System.Net.Http;
using System.Text;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public class LmntEngine : ITtsEngine
{
    private static readonly IReadOnlyList<EngineParameterDef> Schema = new[]
    {
        EngineParameterDef.Slider("temperature", "Expression", 0.0, 1.0, 0.05, 0.5),
    };

    private string _apiKey = string.Empty;
    private readonly HttpClient _http = new() { BaseAddress = new Uri("https://api.lmnt.com") };

    public string EngineId => EngineIds.Lmnt;
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

        var temperature = parameters.Dbl("temperature", 0.5);

        var body = JsonConvert.SerializeObject(new
        {
            text        = text,
            voice       = voiceId,
            format      = "mp3",
            temperature = temperature,
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/ai/speech/bytes")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("X-API-Key", _apiKey);
        request.Headers.TryAddWithoutValidation("lmnt-version", "1.2");

        var response = await _http.SendAsync(request);
        var bytes    = await response.Content.ReadAsByteArrayAsync();

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"LMNT TTS failed ({(int)response.StatusCode}): {Encoding.UTF8.GetString(bytes)}");

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

        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/ai/voice/list");
        request.Headers.TryAddWithoutValidation("X-API-Key", _apiKey);
        request.Headers.TryAddWithoutValidation("lmnt-version", "1.2");

        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return Array.Empty<VoiceInfo>();

        var json = await response.Content.ReadAsStringAsync();
        var arr  = JArray.Parse(json);

        return arr.Select(v => new VoiceInfo
        {
            Id     = v["id"]?.Value<string>()       ?? string.Empty,
            Name   = v["name"]?.Value<string>()     ?? string.Empty,
            Locale = v["language"]?.Value<string>() ?? string.Empty,
            Gender = v["gender"]?.Value<string>()   ?? string.Empty,
        })
        .Where(v => !string.IsNullOrEmpty(v.Id))
        .ToList();
    }

    public void Dispose() => _http.Dispose();
}
