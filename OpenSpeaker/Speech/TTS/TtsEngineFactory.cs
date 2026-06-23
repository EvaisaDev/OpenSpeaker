using OpenSpeaker.Infrastructure.Logging;
using OpenSpeaker.Models;
using OpenSpeaker.TTS.Engines;
namespace OpenSpeaker.TTS;

public class TtsEngineFactory
{
    public record Descriptor(string Id, string DisplayName, Func<IAppLogger?, ITtsEngine> Create);

    public static readonly IReadOnlyList<Descriptor> BuiltIn = new Descriptor[]
    {
        new(EngineIds.Sapi5,       "SAPI5",                 _ => new Sapi5Engine()),
        new(EngineIds.Azure,       "Azure",                 _ => new AzureEngine()),
        new(EngineIds.AmazonPolly, "Amazon Polly",          _ => new AmazonPollyEngine()),
        new(EngineIds.GoogleCloud, "Google Cloud TTS",      _ => new GoogleCloudEngine()),
        new(EngineIds.ElevenLabs,  "ElevenLabs.io",         _ => new ElevenLabsEngine()),
        new(EngineIds.TtsMonster,  "TTSMonster",            _ => new TtsMonsterEngine()),
        new(EngineIds.IbmWatson,   "IBM Watson TTS",        _ => new IbmWatsonEngine()),
        new(EngineIds.Acapela,     "Acapela Cloud",         _ => new AcapelaEngine()),
        new(EngineIds.CereProc,    "CereProc Web Services", _ => new CereProcEngine()),
        new(EngineIds.UberDuck,    "Uberduck",              _ => new UberDuckEngine()),
        new(EngineIds.TikTok,      "TikTok TTS",            _ => new TikTokEngine()),
        new(EngineIds.FakeYou,     "FakeYou",               _ => new FakeYouEngine()),
        new(EngineIds.FishAudio,   "Fish Audio",            log => new FishAudioEngine(log)),
        new(EngineIds.Inworld,     "Inworld TTS",           log => new InworldEngine(log)),
        new(EngineIds.Resemble,    "Resemble AI",           log => new ResembleEngine(log)),
        new(EngineIds.Cartesia,    "Cartesia",              log => new CartesiaEngine(log)),
        new(EngineIds.Lmnt,        "LMNT",                  log => new LmntEngine(log)),
    };

    private static readonly Dictionary<string, Descriptor> _byId =
        BuiltIn.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);

    public static string GetDisplayName(string id) =>
        _byId.TryGetValue(id, out var d) ? d.DisplayName : id;

    public ITtsEngine Create(EngineConfig config, IAppLogger? logger = null)
    {
        var engine = _byId.TryGetValue(config.EngineId, out var d)
            ? d.Create(logger)
            : new Sapi5Engine();

        if (!string.IsNullOrEmpty(config.ConfigJson) && config.ConfigJson != "{}")
            engine.Configure(config.ConfigJson);

        return engine;
    }
}
