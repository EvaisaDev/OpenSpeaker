using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Infrastructure.Logging;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public class FishAudioEngine : HttpTtsEngine, IVoiceSearchEngine
{
    public const string VoiceId   = "fishaudio";
    public const string VoiceName = "FishAudioTTS";

    private static readonly IReadOnlyList<EngineParameterDef> Schema = new[]
    {
        EngineParameterDef.Slider("speed", "Speed", 0.5, 2.0, 0.05, 1.0),
        EngineParameterDef.Combo("model", "Model",
            ["s2.1-pro", "s2.1-pro-free", "s2-pro", "s1"],
            "s2.1-pro"),
        EngineParameterDef.SearchableVoice("voice", "Voice"),
    };

    public FishAudioEngine(IAppLogger? logger = null)
        : base("fishaudio", "https://api.fish.audio", logger) { }

    public override string EngineId => EngineIds.FishAudio;
    public override IReadOnlyList<EngineParameterDef> GetParameters() => Schema;

    protected override void ApplyAuth(HttpRequestMessage request) =>
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

    public override async Task<AudioData> SynthesizeAsync(string text, string voiceId, SynthParams parameters)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(text)) return AudioData.Empty;

        var referenceId = parameters.Str("voice", string.Empty);
        if (string.IsNullOrEmpty(referenceId))
        {
            Logger?.Warn("Fish: no voice selected for FishAudioTTS, nothing to synthesize");
            return AudioData.Empty;
        }

        var speed = parameters.Dbl("speed", 1.0);
        var model = parameters.Str("model", "s2.1-pro");

        var bytes = await PostJsonForBytesAsync("/v1/tts", new
        {
            text,
            reference_id = referenceId,
            format       = "mp3",
            prosody      = new { speed },
            latency      = "normal",
        }, req => req.Headers.TryAddWithoutValidation("model", model));
        return await AudioDecoder.DecodeAsync(bytes);
    }

    public override Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync()
    {
        Logger?.Info($"Fish: returning single '{VoiceName}' pseudo-voice (configured={IsConfigured})");
        if (!IsConfigured) return Task.FromResult<IReadOnlyList<VoiceInfo>>(Array.Empty<VoiceInfo>());

        return Task.FromResult<IReadOnlyList<VoiceInfo>>(new[]
        {
            new VoiceInfo { Id = VoiceId, Name = VoiceName, EngineId = EngineId },
        });
    }

    public Task<IReadOnlyList<VoiceInfo>> TopVoicesAsync(int limit) =>
        FetchModelsAsync($"/model?page_size={limit}&page_number=1&sort_by=task_count");

    public Task<IReadOnlyList<VoiceInfo>> SearchVoicesAsync(string query, int limit)
    {
        if (string.IsNullOrWhiteSpace(query)) return TopVoicesAsync(limit);
        return FetchModelsAsync($"/model?title={Uri.EscapeDataString(query)}&page_size={limit}&page_number=1&sort_by=task_count");
    }

    public async Task<VoiceInfo?> ResolveVoiceAsync(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (await GetJsonAsync($"/model/{id}") is not JObject obj) return null;
        return ToVoice(obj);
    }

    private async Task<IReadOnlyList<VoiceInfo>> FetchModelsAsync(string url)
    {
        if (!IsConfigured) return Array.Empty<VoiceInfo>();

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            ApplyAuth(request);
            var response = await Http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                Logger?.Warn($"Fish: voice fetch failed ({(int)response.StatusCode}): {(body.Length > 300 ? body[..300] : body)}");
                return Array.Empty<VoiceInfo>();
            }

            var voices = new List<VoiceInfo>();
            foreach (var item in JObject.Parse(body)["items"] as JArray ?? new JArray())
                if (ToVoice(item) is { } voice) voices.Add(voice);
            return voices;
        }
        catch (Exception ex)
        {
            Logger?.Error("Fish: voice fetch threw", ex);
            return Array.Empty<VoiceInfo>();
        }
    }

    private static VoiceInfo? ToVoice(JToken item)
    {
        var id = item["_id"]?.Value<string>() ?? item["id"]?.Value<string>() ?? string.Empty;
        if (string.IsNullOrEmpty(id)) return null;
        var lang = (item["languages"] as JArray)?.FirstOrDefault()?.Value<string>()
                ?? (item["language"] as JArray)?.FirstOrDefault()?.Value<string>()
                ?? string.Empty;
        return new VoiceInfo
        {
            Id        = id,
            Name      = item["title"]?.Value<string>() ?? id,
            Locale    = lang,
            Author    = item["author"]?["nickname"]?.Value<string>() ?? string.Empty,
            LikeCount = item["like_count"]?.Value<int?>() ?? 0,
        };
    }
}
