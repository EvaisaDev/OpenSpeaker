using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using NAudio.Wave;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Infrastructure.Http;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public class CereProcEngine : ITtsEngine
{
    private static readonly IReadOnlyList<EngineParameterDef> Schema = new[]
    {
        EngineParameterDef.Slider("speed", "Speed", -20, 20, 1, 0.0),
        EngineParameterDef.Slider("pitch", "Pitch", -20, 20, 1, 1.0)
    };

    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _token = string.Empty;
    private readonly HttpClient _http = HttpClientFactory.GetClient("cereproc", "https://api.cerevoice.com");

    public string EngineId => EngineIds.CereProc;
    public bool IsConfigured => !string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password);

    public IReadOnlyList<EngineParameterDef> GetParameters() => Schema;

    public void Configure(string configJson)
    {
        var obj = JObject.Parse(configJson);
        _username = obj["username"]?.Value<string>() ?? string.Empty;
        _password = obj["password"]?.Value<string>() ?? string.Empty;
        _token    = string.Empty;
    }

    private async Task<string> GetTokenAsync()
    {
        if (!string.IsNullOrEmpty(_token)) return _token;

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_username}:{_password}"));
        var request = new HttpRequestMessage(HttpMethod.Get, "/v2/auth");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new Exception($"CereProc auth failed ({(int)response.StatusCode}): {json}");

        _token = JObject.Parse(json)["access_token"]?.Value<string>() ?? string.Empty;
        if (string.IsNullOrEmpty(_token))
            throw new Exception($"CereProc auth: no access_token in response: {json}");

        return _token;
    }

    private async Task<HttpResponseMessage> AuthedGetAsync(string url)
    {
        var token = await GetTokenAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _http.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _token = string.Empty;
            token = await GetTokenAsync();
            request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            response = await _http.SendAsync(request);
        }
        return response;
    }

    private async Task<HttpResponseMessage> AuthedPostAsync(string url, HttpContent content)
    {
        var token = await GetTokenAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _http.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _token = string.Empty;
            token = await GetTokenAsync();
            request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            response = await _http.SendAsync(request);
        }
        return response;
    }

    private static string BuildSsml(string text, SynthParams parameters)
    {
        var speed = parameters.Dbl("speed", 0.0);
        var pitch = parameters.Dbl("pitch", 1.0);
        var ratePct  = Math.Clamp(100.0 + speed * 5.0, 20.0, 200.0);
        var pitchPct = Math.Clamp((pitch - 1.0) * 5.0, -100.0, 100.0);
        var rateStr  = $"{ratePct:F0}%";
        var pitchStr = pitchPct >= 0 ? $"+{pitchPct:F0}%" : $"{pitchPct:F0}%";
        var inner = $"""<prosody rate="{rateStr}" pitch="{pitchStr}">{System.Security.SecurityElement.Escape(text)}</prosody>""";
        return $"""<speak xmlns="http://www.w3.org/2001/10/synthesis">{inner}</speak>""";
    }

    public async Task<AudioData> SynthesizeAsync(string text, string voiceId, SynthParams parameters)
    {
        if (!IsConfigured) return AudioData.Empty;

        var ssml = BuildSsml(text, parameters);
        var content = new StringContent(ssml, Encoding.UTF8, "application/xml");
        var response = await AuthedPostAsync($"/v2/speak?voice={Uri.EscapeDataString(voiceId)}&audio_format=mp3", content);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        if (!response.IsSuccessStatusCode)
            throw new Exception($"CereProc speak failed ({(int)response.StatusCode}): {Encoding.UTF8.GetString(bytes)}");

        using var ms = new MemoryStream(bytes);
        using var reader = new Mp3FileReader(ms);
        using var pcmStream = WaveFormatConversionStream.CreatePcmStream(reader);
        using var pcmMs = new MemoryStream();
        await pcmStream.CopyToAsync(pcmMs);
        return new AudioData { Samples = pcmMs.ToArray(), Format = pcmStream.WaveFormat };
    }

    public async Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync()
    {
        if (!IsConfigured) return Array.Empty<VoiceInfo>();

        var response = await AuthedGetAsync("/v2/voices");
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new Exception($"CereProc voices failed ({(int)response.StatusCode}): {json}");

        var obj = JObject.Parse(json);
        var arr = obj["voices"] as JArray ?? JArray.Parse(json);
        return arr.Select(v => new VoiceInfo
        {
            Id     = v["name"]?.Value<string>() ?? string.Empty,
            Name   = v["name"]?.Value<string>() ?? string.Empty,
            Locale = $"{v["language_iso"]?.Value<string>() ?? "en"}-{v["country_iso"]?.Value<string>() ?? "GB"}",
            Gender = v["gender"]?.Value<string>() ?? string.Empty
        }).Where(v => !string.IsNullOrEmpty(v.Id)).ToList();
    }

    public void Dispose() { }
}
