using OpenSpeaker.Data;
using OpenSpeaker.Models;
using OpenSpeaker.TTS.Engines;
namespace OpenSpeaker.TTS;

public class TtsEngineRegistry : IDisposable
{
    private readonly Dictionary<string, ITtsEngine> _engines = new();
    private readonly Dictionary<string, string> _displayNames = new();
    private readonly TtsEngineFactory _factory;
    private readonly DatabaseContext _db;

    private static readonly Dictionary<string, string> KnownNames = new()
    {
        [EngineIds.Sapi5]       = "SAPI5",
        [EngineIds.Azure]       = "Azure Cognitive Services",
        [EngineIds.AmazonPolly] = "Amazon Polly",
        [EngineIds.GoogleCloud] = "Google Cloud TTS",
        [EngineIds.ElevenLabs]  = "ElevenLabs.io",
        [EngineIds.TtsMonster]  = "TTSMonster",
        [EngineIds.IbmWatson]   = "IBM Watson TTS",
        [EngineIds.Acapela]     = "Acapela Cloud",
        [EngineIds.CereProc]    = "CereProc Web Services",
        [EngineIds.UberDuck]    = "Uberduck",
        [EngineIds.TikTok]      = "TikTok TTS",
    };

    public TtsEngineRegistry(DatabaseContext db)
    {
        _db = db;
        _factory = new TtsEngineFactory();
        LoadEngines();
    }

    private void LoadEngines()
    {
        var sapi5Config = _db.EngineConfigs.FindOne(c => c.EngineId == EngineIds.Sapi5)
            ?? new EngineConfig { EngineId = EngineIds.Sapi5, Enabled = true };

        if (!_engines.ContainsKey(EngineIds.Sapi5))
        {
            _engines[EngineIds.Sapi5] = _factory.Create(sapi5Config);
            _displayNames[EngineIds.Sapi5] = KnownNames[EngineIds.Sapi5];
        }

        foreach (var config in _db.EngineConfigs.FindAll().Where(c => c.Enabled && c.EngineId != EngineIds.Sapi5))
        {
            _engines[config.EngineId] = _factory.Create(config);
            _displayNames[config.EngineId] = KnownNames.GetValueOrDefault(config.EngineId, config.EngineId);
        }

        foreach (var def in _db.CustomApis.FindAll().Where(d => d.Enabled))
        {
            var engine = new CustomApiEngine(def);
            _engines[engine.EngineId] = engine;
            _displayNames[engine.EngineId] = def.Name;
        }
    }

    public ITtsEngine? GetEngine(string engineId) =>
        _engines.TryGetValue(engineId, out var engine) ? engine : null;

    public ITtsEngine GetDefaultEngine() =>
        _engines.TryGetValue(EngineIds.Sapi5, out var engine) ? engine : _engines.Values.FirstOrDefault() ?? throw new InvalidOperationException("No TTS engine available.");

    public IReadOnlyList<string> GetEnabledEngineIds() => _engines.Keys.ToList();

    public string GetDisplayName(string engineId) =>
        _displayNames.TryGetValue(engineId, out var name) ? name : engineId;

    public void Reload()
    {
        foreach (var engine in _engines.Values)
            engine.Dispose();
        _engines.Clear();
        _displayNames.Clear();
        LoadEngines();
    }

    public void Dispose()
    {
        foreach (var engine in _engines.Values)
            engine.Dispose();
        _engines.Clear();
    }
}
