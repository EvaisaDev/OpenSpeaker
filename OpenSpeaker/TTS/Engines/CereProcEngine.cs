using System.IO;
using System.Net.Http;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Infrastructure.Http;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public class CereProcEngine : ITtsEngine
{
    private static readonly IReadOnlyList<EngineParameterDef> Schema = new[]
    {
        EngineParameterDef.Slider("speed", "Speed", 0.5, 2.0, 0.05, 1.0),
        EngineParameterDef.Slider("pitch", "Pitch", 0.5, 2.0, 0.05, 1.0)
    };

    private string _apiKey = string.Empty;
    private readonly HttpClient _http = HttpClientFactory.GetClient("cereproc", "https://api.cerevoice.com");

    public string EngineId => EngineIds.CereProc;
    public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);

    public IReadOnlyList<EngineParameterDef> GetParameters() => Schema;

    public void Configure(string configJson)
    {
        var obj = JObject.Parse(configJson);
        _apiKey = obj["apiKey"]?.Value<string>() ?? string.Empty;
    }

    public async Task<AudioData> SynthesizeAsync(string text, string voiceId, SynthParams parameters)
    {
        if (!IsConfigured) return AudioData.Empty;

        var body = new
        {
            text,
            voice = voiceId,
            speed = parameters.Dbl("speed", 1.0),
            pitch = parameters.Dbl("pitch", 1.0)
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/v2/speak")
        {
            Content = new StringContent(JsonConvert.SerializeObject(body), System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");

        try
        {
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return AudioData.Empty;

            var bytes = await response.Content.ReadAsByteArrayAsync();
            using var ms = new MemoryStream(bytes);
            using var reader = new WaveFileReader(ms);
            using var pcmMs = new MemoryStream();
            await reader.CopyToAsync(pcmMs);
            return new AudioData { Samples = pcmMs.ToArray(), Format = reader.WaveFormat };
        }
        catch
        {
            return AudioData.Empty;
        }
    }

    public async Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync()
    {
        if (!IsConfigured) return Array.Empty<VoiceInfo>();

        var request = new HttpRequestMessage(HttpMethod.Get, "/v2/voices");
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");

        try
        {
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return Array.Empty<VoiceInfo>();

            var json = await response.Content.ReadAsStringAsync();
            var arr = JArray.Parse(json);
            return arr.Select(v => new VoiceInfo
            {
                Id = v["id"]?.Value<string>() ?? string.Empty,
                Name = v["name"]?.Value<string>() ?? string.Empty,
                Locale = v["language"]?.Value<string>() ?? string.Empty,
                Gender = v["gender"]?.Value<string>() ?? string.Empty
            }).ToList();
        }
        catch
        {
            return Array.Empty<VoiceInfo>();
        }
    }

    public void Dispose() { }
}
