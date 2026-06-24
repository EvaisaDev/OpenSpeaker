// fakeyou is kil (2026)

using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

/*
public class FakeYouEngine : ITtsEngine
{
    private const string BaseUrl     = "https://api.fakeyou.com";
    private const string StorageBase = "https://storage.googleapis.com/vocodes-public";

    private string _username = string.Empty;
    private string _password = string.Empty;

    private readonly CookieContainer _cookies = new();
    private readonly HttpClient _http;

    public FakeYouEngine()
    {
        var handler = new HttpClientHandler { CookieContainer = _cookies, UseCookies = true };
        _http = new HttpClient(handler) { BaseAddress = new Uri(BaseUrl) };
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Origin", "https://fakeyou.com");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://fakeyou.com/");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
    }

    public string EngineId => EngineIds.FakeYou;
    public bool IsConfigured => true;
    public IReadOnlyList<EngineParameterDef> GetParameters() => Array.Empty<EngineParameterDef>();

    public void Configure(string configJson)
    {
        var obj = JObject.Parse(configJson);
        _username = obj["username"]?.Value<string>() ?? string.Empty;
        _password = obj["password"]?.Value<string>() ?? string.Empty;
        if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
            _ = LoginAsync();
    }

    private async Task LoginAsync()
    {
        try
        {
            var body = JsonConvert.SerializeObject(new { username_or_email = _username, password = _password });
            await _http.PostAsync("/v1/login", new StringContent(body, Encoding.UTF8, "application/json"));
        }
        catch { }
    }

    public async Task<AudioData> SynthesizeAsync(string text, string voiceId, SynthParams parameters)
    {
        if (string.IsNullOrWhiteSpace(text)) return AudioData.Empty;

        var inferBody = JsonConvert.SerializeObject(new
        {
            tts_model_token        = voiceId,
            uuid_idempotency_token = Guid.NewGuid().ToString(),
            inference_text         = text,
        });
        var inferResp = await _http.PostAsync("/tts/inference",
            new StringContent(inferBody, Encoding.UTF8, "application/json"));
        var inferJson = await inferResp.Content.ReadAsStringAsync();

        if (!inferResp.IsSuccessStatusCode)
            throw new HttpRequestException($"FakeYou inference failed ({(int)inferResp.StatusCode}): {inferJson}");

        var inferObj = JObject.Parse(inferJson);
        if (inferObj["success"]?.Value<bool>() != true)
            throw new InvalidOperationException($"FakeYou inference request was not successful: {inferJson}");

        var jobToken = inferObj["inference_job_token"]?.Value<string>()
            ?? throw new InvalidOperationException("FakeYou: no inference_job_token in response");

        string? wavPath = null;
        string? lastStatus = null;
        for (var i = 0; i < 120; i++)
        {
            await Task.Delay(1000);
            var pollResp = await _http.GetAsync($"/tts/job/{jobToken}");
            var pollJson = await pollResp.Content.ReadAsStringAsync();

            if (!pollResp.IsSuccessStatusCode)
                throw new HttpRequestException($"FakeYou poll failed ({(int)pollResp.StatusCode}): {pollJson}");

            var state  = JObject.Parse(pollJson)?["state"];
            var status = state?["status"]?.Value<string>();
            lastStatus = status;

            if (status == "complete_success")
            {
                wavPath = state?["maybe_public_bucket_wav_audio_path"]?.Value<string>();
                break;
            }
            if (status is "complete_failure" or "dead")
                throw new InvalidOperationException($"FakeYou job failed with status: {status}");
        }

        if (string.IsNullOrEmpty(wavPath))
            throw new TimeoutException($"FakeYou TTS job timed out (last status: {lastStatus ?? "unknown"})");

        var wavBytes = await _http.GetByteArrayAsync(StorageBase + wavPath);

        return await AudioDecoder.DecodeAsync(wavBytes, "wav");
    }

    public async Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync()
    {
        try
        {
            var resp = await _http.GetAsync("/tts/list");
            var json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) return Array.Empty<VoiceInfo>();

            var arr = JObject.Parse(json)["models"] as JArray ?? new JArray();
            return arr
                .Select(m => new VoiceInfo
                {
                    Id     = m["model_token"]?.Value<string>()  ?? string.Empty,
                    Name   = m["title"]?.Value<string>()        ?? string.Empty,
                    Locale = m["ietf_primary_language_subtag"]?.Value<string>() ?? string.Empty,
                    Gender = string.Empty,
                })
                .Where(v => !string.IsNullOrEmpty(v.Id))
                .ToList();
        }
        catch
        {
            return Array.Empty<VoiceInfo>();
        }
    }

    public void Dispose() => _http.Dispose();
}
*/
