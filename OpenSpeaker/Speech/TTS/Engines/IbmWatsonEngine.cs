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
        var envPath = obj["envFilePath"]?.Value<string>() ?? string.Empty;
        if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
        {
            var env = File.ReadAllLines(envPath)
                .Where(l => l.Contains('=') && !l.TrimStart().StartsWith('#'))
                .Select(l => l.Split('=', 2))
                .ToDictionary(p => p[0].Trim(), p => p[1].Trim());
            env.TryGetValue("TEXT_TO_SPEECH_APIKEY", out _apiKey!);
            if (string.IsNullOrEmpty(_apiKey))
                env.TryGetValue("TEXT_TO_SPEECH_IAM_APIKEY", out _apiKey!);
            env.TryGetValue("TEXT_TO_SPEECH_URL", out _serviceUrl!);
            _apiKey     ??= string.Empty;
            _serviceUrl ??= string.Empty;
        }
    }

    public async Task<AudioData> SynthesizeAsync(string text, string voiceId, SynthParams parameters)
    {
        if (!IsConfigured) return AudioData.Empty;

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"apikey:{_apiKey}"));
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_serviceUrl}/v1/synthesize?voice={Uri.EscapeDataString(voiceId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Headers.Add("Accept", "audio/mp3");
        request.Content = new StringContent(JsonConvert.SerializeObject(new { text }), Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(request);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        if (!response.IsSuccessStatusCode)
            throw new Exception($"IBM Watson synthesize failed ({(int)response.StatusCode}): {Encoding.UTF8.GetString(bytes)}");

        return await AudioDecoder.DecodeAsync(bytes);
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
