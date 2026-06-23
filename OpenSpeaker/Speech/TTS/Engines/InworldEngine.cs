using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Infrastructure.Logging;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public class InworldEngine : HttpTtsEngine
{
    private static readonly IReadOnlyList<EngineParameterDef> Schema = new[]
    {
        EngineParameterDef.Slider("speakingRate", "Speed",   0.5, 1.5, 0.05, 1.0),
        EngineParameterDef.Combo("modelId", "Model",
            ["inworld-tts-2", "inworld-tts-1.5-max", "inworld-tts-1.5-mini"],
            "inworld-tts-2"),
        EngineParameterDef.Combo("deliveryMode", "Delivery",
            ["STABLE", "BALANCED", "CREATIVE"],
            "BALANCED"),
    };

    public InworldEngine(IAppLogger? logger = null)
        : base("inworld", "https://api.inworld.ai", logger) { }

    public override string EngineId => EngineIds.Inworld;
    public override IReadOnlyList<EngineParameterDef> GetParameters() => Schema;

    protected override void ApplyAuth(HttpRequestMessage request) =>
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", ApiKey);

    public override async Task<AudioData> SynthesizeAsync(string text, string voiceId, SynthParams parameters)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(text)) return AudioData.Empty;

        var speakingRate = parameters.Dbl("speakingRate", 1.0);
        var modelId      = parameters.Str("modelId",      "inworld-tts-2");
        var deliveryMode = parameters.Str("deliveryMode", "BALANCED");

        var respText = await PostJsonForStringAsync("/tts/v1/voice", new
        {
            text,
            voiceId,
            modelId,
            deliveryMode,
            audioConfig = new
            {
                encoding        = "MP3",
                sampleRateHertz = 44100,
                speakingRate,
            },
        });

        var b64 = JObject.Parse(respText)["audioContent"]?.Value<string>()
            ?? throw new InvalidOperationException($"Inworld TTS: no audioContent in response: {respText}");

        return await AudioDecoder.DecodeAsync(Convert.FromBase64String(b64));
    }

    public override async Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync()
    {
        if (!IsConfigured) return Array.Empty<VoiceInfo>();

        if (await GetJsonAsync("/tts/v1/voices") is not JToken root) return Array.Empty<VoiceInfo>();
        var arr = root["voices"] as JArray ?? root as JArray ?? new JArray();

        return arr.Select(v => new VoiceInfo
        {
            Id     = v["voiceId"]?.Value<string>()     ?? string.Empty,
            Name   = v["displayName"]?.Value<string>() ?? string.Empty,
            Locale = (v["languages"] as JArray)?.FirstOrDefault()?.Value<string>() ?? string.Empty,
        })
        .Where(v => !string.IsNullOrEmpty(v.Id))
        .ToList();
    }
}
