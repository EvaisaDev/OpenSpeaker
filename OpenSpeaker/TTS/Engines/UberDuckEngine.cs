using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSpeaker.ThingsIDKWhereToPut.Http;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public class UberDuckEngine : ITtsEngine
{
    private string _apiKey = string.Empty;
    private readonly HttpClient _http = HttpClientFactory.GetClient("uberduck", "https://api.uberduck.ai");

    public string EngineId => EngineIds.UberDuck;
    public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);

    public IReadOnlyList<EngineParameterDef> GetParameters() => Array.Empty<EngineParameterDef>();

    public void Configure(string configJson)
    {
        var obj = JObject.Parse(configJson);
        _apiKey = obj["apiKey"]?.Value<string>() ?? string.Empty;
    }

    private void SetAuth(HttpRequestMessage request) =>
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

    public async Task<AudioData> SynthesizeAsync(string text, string voiceId, SynthParams parameters)
    {
        if (!IsConfigured) return AudioData.Empty;

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/text-to-speech")
        {
            Content = new StringContent(
                JsonConvert.SerializeObject(new { text, voice = voiceId }),
                Encoding.UTF8, "application/json")
        };
        SetAuth(request);

        try
        {
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return AudioData.Empty;

            var json = await response.Content.ReadAsStringAsync();
            var audioUrl = JObject.Parse(json)["audio_url"]?.Value<string>();
            if (string.IsNullOrEmpty(audioUrl)) return AudioData.Empty;

            var audioBytes = await _http.GetByteArrayAsync(audioUrl);
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
        if (!IsConfigured) return Array.Empty<VoiceInfo>();

        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/voices");
        SetAuth(request);

        try
        {
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return Array.Empty<VoiceInfo>();

            var json = await response.Content.ReadAsStringAsync();
            var arr = JArray.Parse(json);
            return arr.Select(v => new VoiceInfo
            {
                Id       = v["uuid"]?.Value<string>() ?? v["name"]?.Value<string>() ?? string.Empty,
                Name     = v["display_name"]?.Value<string>() ?? v["name"]?.Value<string>() ?? string.Empty,
                Locale   = v["language"]?.Value<string>() ?? "en-US",
                Gender   = v["gender"]?.Value<string>() ?? "Neutral",
                EngineId = EngineId
            }).Where(v => !string.IsNullOrEmpty(v.Id)).ToList();
        }
        catch
        {
            return Array.Empty<VoiceInfo>();
        }
    }

    public void Dispose() { }
}
