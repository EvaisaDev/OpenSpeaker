using System.Net.Http;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Infrastructure.Logging;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public class CartesiaEngine : HttpTtsEngine
{
    private static readonly IReadOnlyList<EngineParameterDef> Schema = new[]
    {
        EngineParameterDef.Slider("speed",  "Speed",  -1.0, 1.0, 0.05, 0.0),
        EngineParameterDef.Combo("model", "Model",
            ["sonic-3.5", "sonic-3", "sonic-latest"],
            "sonic-3.5"),
    };

    public CartesiaEngine(IAppLogger? logger = null)
        : base("cartesia", "https://api.cartesia.ai", logger) { }

    public override string EngineId => EngineIds.Cartesia;
    public override IReadOnlyList<EngineParameterDef> GetParameters() => Schema;

    protected override void ApplyAuth(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("X-API-Key", ApiKey);
        request.Headers.TryAddWithoutValidation("Cartesia-Version", "2026-03-01");
    }

    public override async Task<AudioData> SynthesizeAsync(string text, string voiceId, SynthParams parameters)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(text)) return AudioData.Empty;

        var speed   = parameters.Dbl("speed", 0.0);
        var modelId = parameters.Str("model", "sonic-3.5");

        var bytes = await PostJsonForBytesAsync("/tts/bytes", new
        {
            model_id   = modelId,
            transcript = text,
            voice      = new { mode = "id", id = voiceId },
            output_format = new
            {
                container   = "mp3",
                bit_rate    = 128000,
                sample_rate = 44100,
            },
            generation_config = new { speed },
        });
        return await AudioDecoder.DecodeAsync(bytes);
    }

    public override async Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync()
    {
        if (!IsConfigured) return Array.Empty<VoiceInfo>();

        return await FetchAllPagesAsync(
            cursor => cursor == null ? "/voices?limit=100" : $"/voices?limit=100&starting_after={cursor}",
            obj => obj["data"] as JArray ?? new JArray(),
            (obj, items, cursor) => (obj["has_more"]?.Value<bool>() ?? false)
                ? items.Last()["id"]?.Value<string>()
                : null,
            v =>
            {
                var id = v["id"]?.Value<string>() ?? string.Empty;
                if (string.IsNullOrEmpty(id)) return null;
                return new VoiceInfo
                {
                    Id     = id,
                    Name   = v["name"]?.Value<string>()     ?? id,
                    Locale = v["language"]?.Value<string>() ?? string.Empty,
                    Gender = v["gender"]?.Value<string>()   ?? string.Empty,
                };
            });
    }
}
