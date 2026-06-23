using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Infrastructure.Http;
using OpenSpeaker.Infrastructure.Logging;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public abstract class HttpTtsEngine : ITtsEngine
{
    protected readonly HttpClient Http;
    protected readonly IAppLogger? Logger;
    protected string ApiKey = string.Empty;

    protected HttpTtsEngine(string clientName, string baseUrl, IAppLogger? logger = null)
    {
        Http = HttpClientFactory.GetClient(clientName, baseUrl);
        Logger = logger;
    }

    public abstract string EngineId { get; }
    public virtual bool IsConfigured => !string.IsNullOrEmpty(ApiKey);
    public abstract IReadOnlyList<EngineParameterDef> GetParameters();

    public virtual void Configure(string configJson) =>
        ApiKey = JObject.Parse(configJson)["apiKey"]?.Value<string>() ?? string.Empty;

    protected abstract void ApplyAuth(HttpRequestMessage request);

    protected async Task<byte[]> PostJsonForBytesAsync(string path, object body)
    {
        var response = await SendJsonAsync(path, body);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"{EngineId} TTS failed ({(int)response.StatusCode}): {Encoding.UTF8.GetString(bytes)}");
        return bytes;
    }

    protected async Task<string> PostJsonForStringAsync(string path, object body)
    {
        var response = await SendJsonAsync(path, body);
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"{EngineId} TTS failed ({(int)response.StatusCode}): {text}");
        return text;
    }

    private async Task<HttpResponseMessage> SendJsonAsync(string path, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json"),
        };
        ApplyAuth(request);
        return await Http.SendAsync(request);
    }

    protected async Task<JToken?> GetJsonAsync(string path, HttpClient? client = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        ApplyAuth(request);
        var response = await (client ?? Http).SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;
        return JToken.Parse(await response.Content.ReadAsStringAsync());
    }

    protected async Task<List<VoiceInfo>> FetchAllPagesAsync(
        Func<string?, string> urlForCursor,
        Func<JObject, JArray> selectItems,
        Func<JObject, JArray, string?, string?> nextCursor,
        Func<JToken, VoiceInfo?> mapVoice,
        HttpClient? client = null)
    {
        var http = client ?? Http;
        var voices = new List<VoiceInfo>();
        string? cursor = null;
        while (true)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, urlForCursor(cursor));
            ApplyAuth(request);
            var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode) break;

            var obj = JObject.Parse(await response.Content.ReadAsStringAsync());
            var items = selectItems(obj);
            foreach (var item in items)
            {
                var voice = mapVoice(item);
                if (voice != null && !string.IsNullOrEmpty(voice.Id)) voices.Add(voice);
            }

            if (items.Count == 0) break;
            cursor = nextCursor(obj, items, cursor);
            if (cursor == null) break;
        }
        return voices;
    }

    public abstract Task<AudioData> SynthesizeAsync(string text, string voiceId, SynthParams parameters);
    public abstract Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync();

    public virtual void Dispose() { }
}
