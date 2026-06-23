using System.IO;
using OpenSpeaker.Data;
using OpenSpeaker.Import;
using OpenSpeaker.ThingsIDKWhereToPut.Logging;
using OpenSpeaker.Models;
namespace OpenSpeaker.Extensions;

public class ExtensionManager : IDisposable
{
    private readonly DatabaseContext _db;
    private readonly IAppLogger? _logger;
    private readonly List<LuaExtension> _extensions = new();

    public static string ExtensionsDirectory =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "extensions");

    public IReadOnlyList<LuaExtension> Extensions => _extensions;

    public IEnumerable<LuaTtsEngine> SpeechEngines =>
        _extensions.SelectMany(e => e.SpeechEngines);

    public ExtensionManager(DatabaseContext db, IAppLogger? logger = null)
    {
        _db = db;
        _logger = logger;
        LoadAll();
    }

    public void Reload()
    {
        foreach (var e in _extensions) e.Dispose();
        _extensions.Clear();
        LoadAll();
    }

    private void LoadAll()
    {
        _logger?.Info($"[ExtensionManager] LoadAll called, directory: {ExtensionsDirectory}");
        if (!Directory.Exists(ExtensionsDirectory))
        {
            _logger?.Warn($"[ExtensionManager] Extensions directory does not exist: {ExtensionsDirectory}");
            return;
        }

        var dirs = Directory.GetDirectories(ExtensionsDirectory);
        _logger?.Info($"[ExtensionManager] Found {dirs.Length} subdirectories in extensions folder");

        foreach (var dir in dirs)
        {
            var mainLua = Path.Combine(dir, "main.lua");
            if (!File.Exists(mainLua))
            {
                _logger?.Info($"[ExtensionManager] Skipping {dir} — no main.lua found");
                continue;
            }

            try
            {
                _logger?.Info($"[ExtensionManager] Loading extension from {mainLua}");
                var ext = Task.Run(() => LuaExtension.CreateAsync(mainLua, _logger)).GetAwaiter().GetResult();
                _logger?.Info($"[ExtensionManager] Loaded extension {ext.ExtensionId} ({ext.DisplayName}), speech engines: [{string.Join(", ", ext.SpeechEngines.Select(e => e.EngineId))}]");
                var saved = _db.ExtensionSettings.FindOne(s => s.ExtensionId == ext.ExtensionId);
                if (saved?.Values is { Count: > 0 })
                    ext.SetSettings(saved.Values);
                _extensions.Add(ext);
            }
            catch (Exception ex)
            {
                _logger?.Error($"[Extensions] Failed to load extension from {dir}: {ex.Message}");
            }
        }

        _logger?.Info($"[ExtensionManager] LoadAll complete, total speech engines registered: [{string.Join(", ", SpeechEngines.Select(e => e.EngineId))}]");
    }

    public void SaveSettings(string extensionId, Dictionary<string, string> values)
    {
        _extensions.FirstOrDefault(e => e.ExtensionId == extensionId)?.SetSettings(values);

        var existing = _db.ExtensionSettings.FindOne(s => s.ExtensionId == extensionId)
            ?? new ExtensionSettings { ExtensionId = extensionId };
        existing.Values = values;
        _db.ExtensionSettings.Upsert(existing);
    }

    public bool HasMessageFilters => _extensions.Any(e => e.HasMessageFilter);

    public async Task<string> ProcessMessageAsync(MessageFilterContext ctx, string message)
    {
        foreach (var ext in _extensions.Where(e => e.HasMessageFilter))
            message = await ext.ProcessMessageAsync(ctx, message);
        return message;
    }

    public IReadOnlyList<ExtAuthField> GetAuthFields(string engineId) =>
        SpeechEngines.FirstOrDefault(e => e.EngineId == engineId)?.AuthFields ?? Array.Empty<ExtAuthField>();

    public string GetDisplayName(string engineId) =>
        SpeechEngines.FirstOrDefault(e => e.EngineId == engineId)?.DisplayName ?? engineId;

    public MigrationData CollectMigrationData()
    {
        var voiceRemap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var engineConfigs = new Dictionary<string, MigrationEngineConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var ext in _extensions)
        {
            foreach (var kvp in ext.MigrationVoiceRemap)
                voiceRemap[kvp.Key] = kvp.Value;
            foreach (var kvp in ext.MigrationEngineConfigs)
                engineConfigs[kvp.Key] = kvp.Value;
        }
        return new MigrationData(voiceRemap, engineConfigs);
    }

    public void Dispose()
    {
        foreach (var e in _extensions) e.Dispose();
        _extensions.Clear();
    }
}
