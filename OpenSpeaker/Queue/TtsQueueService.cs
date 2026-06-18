using OpenSpeaker.Audio;
using OpenSpeaker.Data;
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
    private readonly SemaphoreSlim _pauseSemaphore = new(1, 1);
    private readonly object _pauseLock = new();
    private (string VoiceId, string EngineId) _lastUsedVoice;
    private string _currentUserId = string.Empty;

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
        UserService userService)
    {
        _engineRegistry = engineRegistry;
        _audioPlayer = audioPlayer;
        _playerFactory = playerFactory;
        _wavSaver = wavSaver;
        _settingsRepo = settingsRepo;
        _aliasRepo = aliasRepo;
        _userService = userService;

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
            if (settings.SimultaneousMode)
                _ = Task.Run(() => ProcessItem(item, _playerFactory()));
            else
                await ProcessItem(item, null);
        }
    }

    private async Task ProcessItem(TtsQueueItem item, IAudioPlayer? playerOverride)
    {
        var settings = _settingsRepo.GetSettings();

        ITtsEngine engine;
        string voiceId;
        SynthParams synthParams;
        string deviceId;

        if (!string.IsNullOrEmpty(item.StickyVoiceEngineId))
        {
            engine = _engineRegistry.GetEngine(item.StickyVoiceEngineId) ?? _engineRegistry.GetDefaultEngine();
            voiceId = item.StickyVoiceId;
            synthParams = SynthParams.Empty;
            deviceId = settings.AudioOutputDeviceId;
        }
        else
        {
            var alias = _aliasRepo.GetByName(item.VoiceAliasName)
                ?? _aliasRepo.GetByName(settings.DefaultVoiceAlias)
                ?? new VoiceAlias();
            engine = _engineRegistry.GetEngine(alias.EngineId) ?? _engineRegistry.GetDefaultEngine();
            voiceId = alias.VoiceId;
            synthParams = SynthParams.FromJson(alias.EngineParamsJson);
            deviceId = !string.IsNullOrEmpty(alias.OutputDeviceId) ? alias.OutputDeviceId : settings.AudioOutputDeviceId;
        }

        _lastUsedVoice = (voiceId, engine.EngineId);
        _currentUserId = item.UserId;
        if (!string.IsNullOrEmpty(item.UserId) && !string.IsNullOrEmpty(voiceId))
            _ = _userService.AddPastVoiceAsync(item.UserId, voiceId, engine.EngineId);

        ItemStarted?.Invoke(this, new QueueItemEventArgs { Item = item });

        try
        {
            var audio = await engine.SynthesizeAsync(item.Text, voiceId, synthParams);

            if (audio.IsEmpty) return;

            string? savedPath = null;
            if (settings.SaveTts && !string.IsNullOrEmpty(settings.SaveTtsFolder))
                savedPath = _wavSaver.Save(audio, settings.SaveTtsFolder);

            if (!item.IsSilent && !settings.DisableAudioOutput)
                await (playerOverride ?? _audioPlayer).PlayAsync(audio, deviceId, settings.ApplicationVolume);

            ItemCompleted?.Invoke(this, new QueueItemEventArgs
            {
                Item = item,
                OutputFilePath = savedPath,
                Duration = audio.Duration
            });
        }
        catch
        {
            ItemCompleted?.Invoke(this, new QueueItemEventArgs { Item = item });
        }
        finally
        {
            playerOverride?.Dispose();
        }
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
