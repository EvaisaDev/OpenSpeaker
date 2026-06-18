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

public class UberDuckEngine : ITtsEngine
{
    private string _apiKey = string.Empty;
    private string _apiSecret = string.Empty;
    private readonly HttpClient _http = HttpClientFactory.GetClient("uberduck", "https://api.uberduck.ai");

    public string EngineId => EngineIds.UberDuck;
    public bool IsConfigured => !string.IsNullOrEmpty(_apiKey) && !string.IsNullOrEmpty(_apiSecret);

    public IReadOnlyList<EngineParameterDef> GetParameters() => Array.Empty<EngineParameterDef>();

    public void Configure(string configJson)
    {
        var obj = JObject.Parse(configJson);
        _apiKey = obj["apiKey"]?.Value<string>() ?? string.Empty;
        _apiSecret = obj["apiSecret"]?.Value<string>() ?? string.Empty;
    }

    private void SetAuth(HttpRequestMessage request)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_apiKey}:{_apiSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }

    public async Task<AudioData> SynthesizeAsync(string text, string voiceId, SynthParams parameters)
    {
        if (!IsConfigured) return AudioData.Empty;

        var speakBody = new { speech = text, voice = voiceId };
        var speakRequest = new HttpRequestMessage(HttpMethod.Post, "/speak")
        {
            Content = new StringContent(JsonConvert.SerializeObject(speakBody), Encoding.UTF8, "application/json")
        };
        SetAuth(speakRequest);

        try
        {
            var speakResponse = await _http.SendAsync(speakRequest);
            if (!speakResponse.IsSuccessStatusCode) return AudioData.Empty;

            var speakJson = await speakResponse.Content.ReadAsStringAsync();
            var uuid = JObject.Parse(speakJson)["uuid"]?.Value<string>();
            if (string.IsNullOrEmpty(uuid)) return AudioData.Empty;

            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(1000);
                var statusRequest = new HttpRequestMessage(HttpMethod.Get, $"/speak-status?uuid={uuid}");
                SetAuth(statusRequest);
                var statusResponse = await _http.SendAsync(statusRequest);
                if (!statusResponse.IsSuccessStatusCode) continue;

                var statusJson = await statusResponse.Content.ReadAsStringAsync();
                var statusObj = JObject.Parse(statusJson);
                var path = statusObj["path"]?.Value<string>();
                if (!string.IsNullOrEmpty(path))
                {
                    var audioBytes = await _http.GetByteArrayAsync(path);
                    using var ms = new MemoryStream(audioBytes);
                    using var reader = new Mp3FileReader(ms);
                    using var pcmStream = WaveFormatConversionStream.CreatePcmStream(reader);
                    using var pcmMs = new MemoryStream();
                    await pcmStream.CopyToAsync(pcmMs);
                    return new AudioData { Samples = pcmMs.ToArray(), Format = pcmStream.WaveFormat };
                }
            }

            return AudioData.Empty;
        }
        catch
        {
            return AudioData.Empty;
        }
    }

    public async Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync()
    {
        if (!IsConfigured) return Array.Empty<VoiceInfo>();

        var request = new HttpRequestMessage(HttpMethod.Get, "/voices");
        SetAuth(request);

        try
        {
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return Array.Empty<VoiceInfo>();

            var json = await response.Content.ReadAsStringAsync();
            var arr = JArray.Parse(json);
            return arr.Select(v => new VoiceInfo
            {
                Id = v["name"]?.Value<string>() ?? string.Empty,
                Name = v["display_name"]?.Value<string>() ?? v["name"]?.Value<string>() ?? string.Empty,
                Locale = "en-US",
                Gender = v["category"]?.Value<string>() ?? "Neutral"
            }).ToList();
        }
        catch
        {
            return Array.Empty<VoiceInfo>();
        }
    }

    public void Dispose() { }
}
