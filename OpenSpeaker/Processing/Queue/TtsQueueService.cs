using OpenSpeaker.Audio;
using OpenSpeaker.Data;
using OpenSpeaker.Infrastructure.Logging;
using OpenSpeaker.Models;
using OpenSpeaker.TTS;
using OpenSpeaker.Users;
using System.Collections.Concurrent;
namespace OpenSpeaker.Queue;

public class TtsQueueService : ITtsQueue, IDisposable
{
    private readonly BlockingCollection<TtsQueueItem> _queue = new();
    private readonly TtsEngineRegistry _engineRegistry;
    private readonly IAudioPlayer _audioPlayer;
    private readonly Func<IAudioPlayer> _playerFactory;
    private readonly WavFileSaver _wavSaver;
    private readonly SettingsRepository _settingsRepo;
    private readonly VoiceAliasRepository _aliasRepo;
    private readonly UserService _userService;
    private readonly CancellationTokenSource _cts = new();
    private bool _paused = false;
    private readonly object _pauseLock = new();
    private (string VoiceId, string EngineId) _lastUsedVoice;
    private string _currentUserId = string.Empty;
    private readonly IAppLogger? _logger;
    private Task _pregenTail = Task.CompletedTask;

    public event EventHandler<QueueItemEventArgs>? ItemStarted;
    public event EventHandler<QueueItemEventArgs>? ItemCompleted;

    public bool IsPaused => _paused;
    public int Count => _queue.Count;
    public (string VoiceId, string EngineId) LastUsedVoice => _lastUsedVoice;

    public TtsQueueService(
        TtsEngineRegistry engineRegistry,
        IAudioPlayer audioPlayer,
        Func<IAudioPlayer> playerFactory,
        WavFileSaver wavSaver,
        SettingsRepository settingsRepo,
        VoiceAliasRepository aliasRepo,
        UserService userService,
        IAppLogger? logger = null)
    {
        _engineRegistry = engineRegistry;
        _audioPlayer = audioPlayer;
        _playerFactory = playerFactory;
        _wavSaver = wavSaver;
        _settingsRepo = settingsRepo;
        _aliasRepo = aliasRepo;
        _userService = userService;
        _logger = logger;

        Task.Run(ProcessLoop);
    }

    private async Task ProcessLoop()
    {
        foreach (var item in _queue.GetConsumingEnumerable(_cts.Token))
        {
            while (_paused && !_cts.IsCancellationRequested)
                await Task.Delay(100);

            if (_cts.IsCancellationRequested) break;

            var settings = _settingsRepo.GetSettings();
            switch (settings.QueueMode)
            {
                case QueueModes.Simultaneous:
                    _ = Task.Run(() => ProcessItem(item, _playerFactory()));
                    break;

                case QueueModes.PreGenerated:
                    var capturedItem = item;
                    var synthTask = SynthesizeItemAsync(capturedItem);
                    var prevTail = _pregenTail;
                    _pregenTail = Task.Run(async () =>
                    {
                        await prevTail;
                        var result = await synthTask;
                        if (result != null)
                            await PlaySynthesisResultAsync(result, null);
                    });
                    break;

                default:
                    await ProcessItem(item, null);
                    break;
            }
        }
    }

    private record SynthesisResult(
        TtsQueueItem Item,
        AudioData Audio,
        string DeviceId,
        string? SavedPath);

    private async Task<SynthesisResult?> SynthesizeItemAsync(TtsQueueItem item)
    {
        var settings = _settingsRepo.GetSettings();

        ITtsEngine engine;
        string voiceId;
        SynthParams synthParams;
        string deviceId;
        string aliasName;

        if (!string.IsNullOrEmpty(item.StickyVoiceEngineId))
        {
            engine     = _engineRegistry.GetEngine(item.StickyVoiceEngineId) ?? _engineRegistry.GetDefaultEngine();
            voiceId    = item.StickyVoiceId;
            synthParams = SynthParams.Empty;
            deviceId   = settings.AudioOutputDeviceId;
            aliasName  = item.VoiceAliasName;
        }
        else
        {
            var alias  = _aliasRepo.GetByName(item.VoiceAliasName)
                ?? _aliasRepo.GetByName(settings.DefaultVoiceAlias)
                ?? new VoiceAlias();
            engine     = _engineRegistry.GetEngine(alias.EngineId) ?? _engineRegistry.GetDefaultEngine();
            voiceId    = alias.VoiceId;
            synthParams = SynthParams.FromJson(alias.EngineParamsJson);
            deviceId   = !string.IsNullOrEmpty(alias.OutputDeviceId) ? alias.OutputDeviceId : settings.AudioOutputDeviceId;
            aliasName  = !string.IsNullOrEmpty(alias.Name) ? alias.Name : item.VoiceAliasName;
        }

        _lastUsedVoice = (voiceId, engine.EngineId);
        _currentUserId = item.UserId;
        if (!string.IsNullOrEmpty(item.UserId) && !string.IsNullOrEmpty(voiceId))
            _ = _userService.AddPastVoiceAsync(item.UserId, voiceId, engine.EngineId);

        _logger?.Info($"QUEUE :: Processing '{item.Text}' engine={engine.EngineId} voiceId='{voiceId}' device='{deviceId}'");
        ItemStarted?.Invoke(this, new QueueItemEventArgs { Item = item });

        try
        {
            var audio = await engine.SynthesizeAsync(item.Text, voiceId, synthParams);
            _logger?.Info($"QUEUE :: Synthesis done. IsEmpty={audio.IsEmpty}");
            if (audio.IsEmpty)
            {
                ItemCompleted?.Invoke(this, new QueueItemEventArgs { Item = item });
                return null;
            }

            string? savedPath = null;
            if (settings.SaveTts && !string.IsNullOrEmpty(settings.SaveTtsFolder))
            {
                var paramValues = engine.GetParameters()
                    .Select(p => synthParams.Str(p.Key, p.Default))
                    .ToList();
                savedPath = _wavSaver.Save(audio, settings.SaveTtsFolder,
                    paramValues, aliasName,
                    string.IsNullOrEmpty(item.Username) ? null : item.Username);
            }

            return new SynthesisResult(item, audio, deviceId, savedPath);
        }
        catch (Exception ex)
        {
            _logger?.Error($"TTS synthesis failed for engine {engine.EngineId}: {ex.Message}");
            ItemCompleted?.Invoke(this, new QueueItemEventArgs { Item = item });
            return null;
        }
    }

    private async Task PlaySynthesisResultAsync(SynthesisResult result, IAudioPlayer? playerOverride)
    {
        var settings = _settingsRepo.GetSettings();
        try
        {
            _logger?.Info($"QUEUE :: IsSilent={result.Item.IsSilent} DisableAudioOutput={settings.DisableAudioOutput}");
            if (!result.Item.IsSilent && !settings.DisableAudioOutput)
                await (playerOverride ?? _audioPlayer).PlayAsync(result.Audio, result.DeviceId, settings.ApplicationVolume);

            ItemCompleted?.Invoke(this, new QueueItemEventArgs
            {
                Item           = result.Item,
                OutputFilePath = result.SavedPath,
                Duration       = result.Audio.Duration,
            });
        }
        catch (Exception ex)
        {
            _logger?.Error($"TTS playback failed: {ex.Message}");
            ItemCompleted?.Invoke(this, new QueueItemEventArgs { Item = result.Item });
        }
        finally
        {
            playerOverride?.Dispose();
        }
    }

    private async Task ProcessItem(TtsQueueItem item, IAudioPlayer? playerOverride)
    {
        var result = await SynthesizeItemAsync(item);
        if (result != null)
            await PlaySynthesisResultAsync(result, playerOverride);
        else
            playerOverride?.Dispose();
    }

    public void Enqueue(TtsQueueItem item) => _queue.TryAdd(item);
    public void Pause() { lock (_pauseLock) { _paused = true; } }
    public void Resume() { lock (_pauseLock) { _paused = false; } }
    public void Clear()
    {
        while (_queue.TryTake(out _)) { }
    }
    public void Stop() => _audioPlayer.Stop();
    public void StopUser(string userId)
    {
        if (_currentUserId == userId) _audioPlayer.Stop();
    }
    public void SkipUser(string userId)
    {
        var kept = new List<TtsQueueItem>();
        while (_queue.TryTake(out var item))
            if (item.UserId != userId) kept.Add(item);
        foreach (var item in kept) _queue.TryAdd(item);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _queue.CompleteAdding();
        _cts.Dispose();
        _queue.Dispose();
    }
}
