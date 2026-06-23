using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Infrastructure.Logging;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public class FishAudioEngine : HttpTtsEngine
{
    private static readonly IReadOnlyList<EngineParameterDef> Schema = new[]
    {
        EngineParameterDef.Slider("speed", "Speed", 0.5, 2.0, 0.05, 1.0),
        EngineParameterDef.Combo("model", "Model", ["s2-pro", "s1"], "s2-pro"),
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

        var speed = parameters.Dbl("speed", 1.0);
        var model = parameters.Str("model", "s2-pro");

        var bytes = await PostJsonForBytesAsync("/v1/tts", new
        {
            text,
            reference_id = voiceId,
            format       = "mp3",
            model,
            prosody      = new { speed },
            latency      = "normal",
        });
        return await AudioDecoder.DecodeAsync(bytes);
    }

    public override async Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync()
    {
        if (!IsConfigured) return Array.Empty<VoiceInfo>();

        return await FetchAllPagesAsync(
            cursor => $"/model?page_size=50&page_number={cursor ?? "1"}&sort_by=task_count",
            obj => obj["items"] as JArray ?? new JArray(),
            (obj, items, cursor) => (obj["has_more"]?.Value<bool>() ?? false)
                ? ((cursor == null ? 1 : int.Parse(cursor)) + 1).ToString()
                : null,
            item =>
            {
                var id = item["_id"]?.Value<string>() ?? item["id"]?.Value<string>() ?? string.Empty;
                if (string.IsNullOrEmpty(id)) return null;
                var lang = (item["languages"] as JArray)?.FirstOrDefault()?.Value<string>()
                        ?? (item["language"] as JArray)?.FirstOrDefault()?.Value<string>()
                        ?? string.Empty;
                return new VoiceInfo
                {
                    Id     = id,
                    Name   = item["title"]?.Value<string>() ?? id,
                    Locale = lang,
                };
            });
    }
}
