using OpenSpeaker.Models;
using OpenSpeaker.TTS.Engines;
namespace OpenSpeaker.TTS;

public class TtsEngineFactory
{
    public record Descriptor(string Id, string DisplayName, Func<ITtsEngine> Create);

    public static readonly IReadOnlyList<Descriptor> BuiltIn = new Descriptor[]
    {
        new(EngineIds.Sapi5,       "SAPI5",                 () => new Sapi5Engine()),
        new(EngineIds.Azure,       "Azure",                 () => new AzureEngine()),
        new(EngineIds.AmazonPolly, "Amazon Polly",          () => new AmazonPollyEngine()),
        new(EngineIds.GoogleCloud, "Google Cloud TTS",      () => new GoogleCloudEngine()),
        new(EngineIds.ElevenLabs,  "ElevenLabs.io",         () => new ElevenLabsEngine()),
        new(EngineIds.TtsMonster,  "TTSMonster",            () => new TtsMonsterEngine()),
        new(EngineIds.IbmWatson,   "IBM Watson TTS",        () => new IbmWatsonEngine()),
        new(EngineIds.Acapela,     "Acapela Cloud",         () => new AcapelaEngine()),
        new(EngineIds.CereProc,    "CereProc Web Services", () => new CereProcEngine()),
        new(EngineIds.UberDuck,    "Uberduck",              () => new UberDuckEngine()),
        new(EngineIds.TikTok,      "TikTok TTS",            () => new TikTokEngine()),
        new(EngineIds.FakeYou,     "FakeYou",               () => new FakeYouEngine()),
        new(EngineIds.FishAudio,   "Fish Audio",            () => new FishAudioEngine()),
        new(EngineIds.Inworld,     "Inworld TTS",           () => new InworldEngine()),
        new(EngineIds.Resemble,    "Resemble AI",           () => new ResembleEngine()),
        new(EngineIds.Cartesia,    "Cartesia",              () => new CartesiaEngine()),
        new(EngineIds.Lmnt,        "LMNT",                  () => new LmntEngine()),
    };

    private static readonly Dictionary<string, Descriptor> _byId =
        BuiltIn.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);

    public static string GetDisplayName(string id) =>
        _byId.TryGetValue(id, out var d) ? d.DisplayName : id;

    public ITtsEngine Create(EngineConfig config)
    {
        var engine = _byId.TryGetValue(config.EngineId, out var d)
            ? d.Create()
            : new Sapi5Engine();

        if (!string.IsNullOrEmpty(config.ConfigJson) && config.ConfigJson != "{}")
            engine.Configure(config.ConfigJson);

        return engine;
    }
}
