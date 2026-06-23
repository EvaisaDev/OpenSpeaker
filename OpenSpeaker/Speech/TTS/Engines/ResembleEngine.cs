using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public class ResembleEngine : ITtsEngine
{
    private static readonly IReadOnlyList<EngineParameterDef> Schema = new[]
    {
        EngineParameterDef.Combo("model", "Model",
            ["chatterbox-turbo", "chatterbox", "chatterbox-multilingual"],
            "chatterbox-turbo"),
    };

    private string _apiKey = string.Empty;
    private readonly HttpClient _http = new() { BaseAddress = new Uri("https://f.cluster.resemble.ai") };
    private readonly HttpClient _apiHttp = new() { BaseAddress = new Uri("https://app.resemble.ai") };

    public string EngineId => EngineIds.Resemble;
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

        var model = parameters.Str("model", "chatterbox-turbo");

        var body = JsonConvert.SerializeObject(new
        {
            voice_uuid    = voiceId,
            data          = text,
            model         = model,
            output_format = "mp3",
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/synthesize")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        var response = await _http.SendAsync(request);
        var respText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Resemble TTS failed ({(int)response.StatusCode}): {respText}");

        var b64 = JObject.Parse(respText)["audio_data"]?.Value<string>()
            ?? throw new InvalidOperationException($"Resemble TTS: no audio_data in response: {respText}");

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

        var voices = new List<VoiceInfo>();
        var page   = 1;

        while (true)
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"/api/v2/voices?page={page}&page_size=100");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _apiHttp.SendAsync(request);
            if (!response.IsSuccessStatusCode) break;

            var json     = await response.Content.ReadAsStringAsync();
            var obj      = JObject.Parse(json);
            var items    = obj["items"] as JArray ?? new JArray();
            var numPages = obj["num_pages"]?.Value<int>() ?? 1;

            foreach (var v in items)
            {
                var id = v["uuid"]?.Value<string>() ?? string.Empty;
                if (string.IsNullOrEmpty(id)) continue;
                voices.Add(new VoiceInfo
                {
                    Id   = id,
                    Name = v["name"]?.Value<string>() ?? id,
                });
            }

            if (page >= numPages) break;
            page++;
        }

        return voices;
    }

    public void Dispose()
    {
        _http.Dispose();
        _apiHttp.Dispose();
    }
}
