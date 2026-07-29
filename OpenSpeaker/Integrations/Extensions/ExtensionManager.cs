using System.IO;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Data;
using OpenSpeaker.Import;
using OpenSpeaker.Infrastructure.Logging;
using OpenSpeaker.Input;
using OpenSpeaker.Models;
namespace OpenSpeaker.Extensions;

public class ExtensionManager : IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(16);

    private readonly DatabaseContext _db;
    private readonly KeybindService? _keybinds;
    private readonly IAppLogger? _logger;
    private volatile List<LuaExtension> _extensions = new();
    private Func<string, Task>? _chatSender;
    private Func<string, int, string, Task<bool>>? _timeoutSender;
    private Func<string, Task>? _soundPlayer;
    private Func<List<QueueEntryInfo>>? _getPlayingQueueItems;
    private Func<List<QueueEntryInfo>>? _getQueuedItems;
    private Func<string, bool>? _stopQueueItem;
    private Func<string, bool>? _removeQueueItem;
    private Action<string>? _wsBroadcaster;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;

    public static string ExtensionsDirectory =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "extensions");

    public IReadOnlyList<LuaExtension> Extensions => _extensions;

    public IEnumerable<LuaTtsEngine> SpeechEngines =>
        _extensions.SelectMany(e => e.SpeechEngines);

    public ExtensionManager(DatabaseContext db, KeybindService? keybinds = null, IAppLogger? logger = null)
    {
        _db = db;
        _keybinds = keybinds;
        _logger = logger;
        LoadAll();
    }

    public void Reload()
    {
        var old = _extensions;
        LoadAll();
        foreach (var e in old) e.Dispose();
        StartUpdateLoop();
    }

    private void LoadAll()
    {
        var loaded = new List<LuaExtension>();
        _logger?.Info($"[ExtensionManager] LoadAll called, directory: {ExtensionsDirectory}");
        if (!Directory.Exists(ExtensionsDirectory))
        {
            _logger?.Warn($"[ExtensionManager] Extensions directory does not exist: {ExtensionsDirectory}");
            _extensions = loaded;
            return;
        }

        var dirs = Directory.GetDirectories(ExtensionsDirectory);
        _logger?.Info($"[ExtensionManager] Found {dirs.Length} subdirectories in extensions folder");

        foreach (var dir in dirs)
        {
            var mainLua = Path.Combine(dir, "main.lua");
            if (!File.Exists(mainLua))
            {
                _logger?.Info($"[ExtensionManager] Skipping {dir} - no main.lua found");
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
                loaded.Add(ext);
            }
            catch (Exception ex)
            {
                _logger?.Error($"[Extensions] Failed to load extension from {dir}: {ex.Message}");
            }
        }

        foreach (var e in loaded)
        {
            e.SetChatSender(_chatSender);
            e.SetTimeoutSender(_timeoutSender);
            e.SetSoundPlayer(_soundPlayer);
            e.SetGetPlayingQueueItems(_getPlayingQueueItems);
            e.SetGetQueuedItems(_getQueuedItems);
            e.SetStopQueueItem(_stopQueueItem);
            e.SetRemoveQueueItem(_removeQueueItem);
            e.SetWsBroadcaster(_wsBroadcaster);
            e.SetKeybinds(_keybinds);
            var extensionId = e.ExtensionId;
            e.SetStorage(
                key => StorageGet(extensionId, key),
                (key, value) => StorageSet(extensionId, key, value),
                key => StorageDelete(extensionId, key));
        }
        _extensions = loaded;
        _logger?.Info($"[ExtensionManager] LoadAll complete, total speech engines registered: [{string.Join(", ", SpeechEngines.Select(e => e.EngineId))}]");
    }

    public void SetChatSender(Func<string, Task>? sender)
    {
        _chatSender = sender;
        foreach (var e in _extensions) e.SetChatSender(sender);
    }

    public void SetTimeoutSender(Func<string, int, string, Task<bool>>? sender)
    {
        _timeoutSender = sender;
        foreach (var e in _extensions) e.SetTimeoutSender(sender);
    }

    public void SetSoundPlayer(Func<string, Task>? player)
    {
        _soundPlayer = player;
        foreach (var e in _extensions) e.SetSoundPlayer(player);
    }

    public void SetGetPlayingQueueItems(Func<List<QueueEntryInfo>>? getter)
    {
        _getPlayingQueueItems = getter;
        foreach (var e in _extensions) e.SetGetPlayingQueueItems(getter);
    }

    public void SetGetQueuedItems(Func<List<QueueEntryInfo>>? getter)
    {
        _getQueuedItems = getter;
        foreach (var e in _extensions) e.SetGetQueuedItems(getter);
    }

    public void SetStopQueueItem(Func<string, bool>? fn)
    {
        _stopQueueItem = fn;
        foreach (var e in _extensions) e.SetStopQueueItem(fn);
    }

    public void SetRemoveQueueItem(Func<string, bool>? fn)
    {
        _removeQueueItem = fn;
        foreach (var e in _extensions) e.SetRemoveQueueItem(fn);
    }

    public void SetWsBroadcaster(Action<string>? broadcaster)
    {
        _wsBroadcaster = broadcaster;
        foreach (var e in _extensions) e.SetWsBroadcaster(broadcaster);
    }

    public void StartUpdateLoop()
    {
        if (_loopCts != null) return;
        if (!_extensions.Any(e => e.NeedsTick)) return;

        _keybinds?.Install();
        _loopCts = new CancellationTokenSource();
        _loopTask = Task.Run(() => RunUpdateLoopAsync(_loopCts.Token));
    }

    private async Task RunUpdateLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                _keybinds?.Tick();
                foreach (var ext in _extensions)
                    if (ext.HasUpdate)
                        await ext.UpdateAsync();
            }
            catch (Exception ex) { _logger?.Error($"[ExtensionManager] Update loop error: {ex.Message}"); }

            try { await Task.Delay(TickInterval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private string? StorageGet(string extensionId, string key) =>
        _db.ExtensionData.FindOne(d => d.ExtensionId == extensionId && d.Key == key)?.Value;

    private void StorageSet(string extensionId, string key, string value)
    {
        var existing = _db.ExtensionData.FindOne(d => d.ExtensionId == extensionId && d.Key == key);
        if (existing is null)
        {
            _db.ExtensionData.Insert(new ExtensionData { ExtensionId = extensionId, Key = key, Value = value });
        }
        else
        {
            existing.Value = value;
            _db.ExtensionData.Update(existing);
        }
    }

    private void StorageDelete(string extensionId, string key) =>
        _db.ExtensionData.DeleteMany(d => d.ExtensionId == extensionId && d.Key == key);

    public void SaveSettings(string extensionId, Dictionary<string, string> values)
    {
        _extensions.FirstOrDefault(e => e.ExtensionId == extensionId)?.SetSettings(values);

        var existing = _db.ExtensionSettings.FindOne(s => s.ExtensionId == extensionId)
            ?? new ExtensionSettings { ExtensionId = extensionId };
        existing.Values = values;
        _db.ExtensionSettings.Upsert(existing);
    }

    public bool HasMessageFilters => _extensions.Any(e => e.HasMessageFilter);

    public bool HasChatObservers => _extensions.Any(e => e.HasChatObserver);

    public async Task ObserveMessageAsync(MessageFilterContext ctx, string message)
    {
        var snapshot = _extensions;
        foreach (var ext in snapshot.Where(e => e.HasChatObserver))
            await ext.ObserveMessageAsync(ctx, message);
    }

    public async Task<string> ProcessMessageAsync(MessageFilterContext ctx, string message)
    {
        var snapshot = _extensions;
        foreach (var ext in snapshot.Where(e => e.HasMessageFilter))
            message = await ext.ProcessMessageAsync(ctx, message);
        return message;
    }

    public bool HasBeforeSpeakHooks => _extensions.Any(e => e.HasBeforeSpeak);

    public async Task<string?> InvokeBeforeSpeakAsync(string userId, string username, string audioBase64Wav)
    {
        var snapshot = _extensions;
        foreach (var ext in snapshot.Where(e => e.HasBeforeSpeak))
        {
            var action = await ext.InvokeBeforeSpeakAsync(userId, username, audioBase64Wav);
            if (!string.IsNullOrEmpty(action)) return action;
        }
        return null;
    }

    public bool HasWsCommandHandlers => _extensions.Any(e => e.HasWsCommand);

    public async Task<JToken?> DispatchWsCommandAsync(string command, JObject data)
    {
        var snapshot = _extensions;
        foreach (var ext in snapshot.Where(e => e.HasWsCommand))
        {
            var result = await ext.HandleWsCommandAsync(command, data);
            if (result is not null) return result;
        }
        return null;
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
        _loopCts?.Cancel();
        try { _loopTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _loopCts?.Dispose();
        _loopCts = null;

        var old = _extensions;
        _extensions = new List<LuaExtension>();
        foreach (var e in old) e.Dispose();
    }
}
