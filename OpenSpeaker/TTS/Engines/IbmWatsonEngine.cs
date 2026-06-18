using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Infrastructure.Http;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public class IbmWatsonEngine : ITtsEngine
{
    private string _apiKey = string.Empty;
    private string _serviceUrl = string.Empty;
    private readonly HttpClient _http = HttpClientFactory.GetClient("ibmwatson");

    public string EngineId => EngineIds.IbmWatson;
    public bool IsConfigured => !string.IsNullOrEmpty(_apiKey) && !string.IsNullOrEmpty(_serviceUrl);

    public IReadOnlyList<EngineParameterDef> GetParameters() => Array.Empty<EngineParameterDef>();

    public void Configure(string configJson)
    {
        var obj = JObject.Parse(configJson);
        _apiKey = obj["apiKey"]?.Value<string>() ?? string.Empty;
        _serviceUrl = obj["serviceUrl"]?.Value<string>() ?? string.Empty;
    }

    public async Task<AudioData> SynthesizeAsync(string text, string voiceId, SynthParams parameters)
    {
        if (!IsConfigured) return AudioData.Empty;

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"apikey:{_apiKey}"));
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_serviceUrl}/v1/synthesize?voice={Uri.EscapeDataString(voiceId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Headers.Add("Accept", "audio/wav");
        request.Content = new StringContent(JsonConvert.SerializeObject(new { text }), Encoding.UTF8, "application/json");

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

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"apikey:{_apiKey}"));
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_serviceUrl}/v1/voices");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        try
        {
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return Array.Empty<VoiceInfo>();

            var json = await response.Content.ReadAsStringAsync();
            var obj = JObject.Parse(json);
            return obj["voices"]!.Select(v => new VoiceInfo
            {
                Id = v["name"]!.Value<string>() ?? string.Empty,
                Name = v["name"]!.Value<string>() ?? string.Empty,
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
