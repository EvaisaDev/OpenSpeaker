using System.Net.Http;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Infrastructure.Logging;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public class LmntEngine : HttpTtsEngine
{
    private static readonly IReadOnlyList<EngineParameterDef> Schema = new[]
    {
        EngineParameterDef.Slider("temperature", "Expression", 0.0, 1.0, 0.05, 0.5),
    };

    public LmntEngine(IAppLogger? logger = null)
        : base("lmnt", "https://api.lmnt.com", logger) { }

    public override string EngineId => EngineIds.Lmnt;
    public override IReadOnlyList<EngineParameterDef> GetParameters() => Schema;

    protected override void ApplyAuth(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("X-API-Key", ApiKey);
        request.Headers.TryAddWithoutValidation("lmnt-version", "1.2");
    }

    public override async Task<AudioData> SynthesizeAsync(string text, string voiceId, SynthParams parameters)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(text)) return AudioData.Empty;

        var temperature = parameters.Dbl("temperature", 0.5);

        var bytes = await PostJsonForBytesAsync("/v1/ai/speech/bytes", new
        {
            text,
            voice       = voiceId,
            format      = "mp3",
            temperature,
        });
        return await AudioDecoder.DecodeAsync(bytes);
    }

    public override async Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync()
    {
        if (!IsConfigured) return Array.Empty<VoiceInfo>();

        if (await GetJsonAsync("/v1/ai/voice/list") is not JArray arr) return Array.Empty<VoiceInfo>();

        return arr.Select(v => new VoiceInfo
        {
            Id     = v["id"]?.Value<string>()       ?? string.Empty,
            Name   = v["name"]?.Value<string>()     ?? string.Empty,
            Locale = v["language"]?.Value<string>() ?? string.Empty,
            Gender = v["gender"]?.Value<string>()   ?? string.Empty,
        })
        .Where(v => !string.IsNullOrEmpty(v.Id))
        .ToList();
    }
}
