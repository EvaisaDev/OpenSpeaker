using OpenSpeaker.Data;
using OpenSpeaker.Extensions;
using OpenSpeaker.ThingsIDKWhereToPut.Logging;
using OpenSpeaker.Models;
using OpenSpeaker.TTS.Engines;
namespace OpenSpeaker.TTS;

public class TtsEngineRegistry : IDisposable
{
    private readonly Dictionary<string, ITtsEngine> _engines = new();
    private readonly Dictionary<string, string> _displayNames = new();
    private readonly TtsEngineFactory _factory;
    private readonly DatabaseContext _db;
    private readonly ExtensionManager? _extensions;
    private readonly IAppLogger? _logger;


    public TtsEngineRegistry(DatabaseContext db, ExtensionManager? extensions = null, IAppLogger? logger = null)
    {
        _db = db;
        _extensions = extensions;
        _logger = logger;
        _factory = new TtsEngineFactory();
        LoadEngines();
    }

    private void LoadEngines()
    {
        _logger?.Info("[TtsEngineRegistry] LoadEngines called");

        var sapi5Config = _db.EngineConfigs.FindOne(c => c.EngineId == EngineIds.Sapi5)
            ?? new EngineConfig { EngineId = EngineIds.Sapi5, Enabled = true };

        if (!_engines.ContainsKey(EngineIds.Sapi5))
        {
            _engines[EngineIds.Sapi5] = _factory.Create(sapi5Config);
            _displayNames[EngineIds.Sapi5] = TtsEngineFactory.GetDisplayName(EngineIds.Sapi5);
        }

        var allExtEngines = _extensions?.SpeechEngines.Select(e => e.EngineId).ToList() ?? new List<string>();
        _logger?.Info($"[TtsEngineRegistry] Available extension speech engines: [{string.Join(", ", allExtEngines)}]");

        var enabledConfigs = _db.EngineConfigs.FindAll().Where(c => c.Enabled && c.EngineId != EngineIds.Sapi5).ToList();
        _logger?.Info($"[TtsEngineRegistry] Enabled engine configs in DB: [{string.Join(", ", enabledConfigs.Select(c => c.EngineId))}]");

        foreach (var config in enabledConfigs)
        {
            if (config.EngineId.StartsWith("ext:", StringComparison.Ordinal) && _extensions != null)
            {
                _logger?.Info($"[TtsEngineRegistry] Processing ext engine: engineId={config.EngineId} configJson={config.ConfigJson ?? "(null)"}");
                var luaEngine = _extensions.SpeechEngines.FirstOrDefault(e => e.EngineId == config.EngineId);
                if (luaEngine == null)
                {
                    _logger?.Warn($"[TtsEngineRegistry] No LuaTtsEngine found for {config.EngineId} — extension may not be loaded");
                    continue;
                }
                _logger?.Info($"[TtsEngineRegistry] Found LuaTtsEngine for {config.EngineId}, calling Configure with configJson={config.ConfigJson ?? "{}"}");
                luaEngine.Configure(config.ConfigJson ?? "{}");
                _engines[luaEngine.EngineId] = luaEngine;
                _displayNames[luaEngine.EngineId] = luaEngine.DisplayName;
                _logger?.Info($"[TtsEngineRegistry] Registered ext engine {luaEngine.EngineId} ({luaEngine.DisplayName})");
            }
            else
            {
                _engines[config.EngineId] = _factory.Create(config);
                _displayNames[config.EngineId] = TtsEngineFactory.GetDisplayName(config.EngineId);
            }
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
        foreach (var kv in _engines)
            if (!kv.Key.StartsWith("ext:", StringComparison.Ordinal))
                kv.Value.Dispose();
        _engines.Clear();
        _displayNames.Clear();
        LoadEngines();
    }

    public void Dispose()
    {
        foreach (var kv in _engines)
            if (!kv.Key.StartsWith("ext:", StringComparison.Ordinal))
                kv.Value.Dispose();
        _engines.Clear();
    }
}
