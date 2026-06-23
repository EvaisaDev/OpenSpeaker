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
    private readonly TtsSynthesizer _synthesizer;
    private readonly PlaybackCoordinator _playback;
    private readonly Func<IAudioPlayer> _playerFactory;
    private readonly SettingsRepository _settingsRepo;
    private readonly CancellationTokenSource _cts = new();
    private bool _paused = false;
    private readonly object _pauseLock = new();
    private readonly IAppLogger? _logger;
    private Task _pregenTail = Task.CompletedTask;

    public event EventHandler<QueueItemEventArgs>? ItemStarted;
    public event EventHandler<QueueItemEventArgs>? ItemCompleted;

    public bool IsPaused => _paused;
    public int Count => _queue.Count;
    public (string VoiceId, string EngineId) LastUsedVoice => _synthesizer.LastUsedVoice;

    public TtsQueueService(
        TtsSynthesizer synthesizer,
        PlaybackCoordinator playback,
        Func<IAudioPlayer> playerFactory,
        SettingsRepository settingsRepo,
        IAppLogger? logger = null)
    {
        _synthesizer = synthesizer;
        _playback = playback;
        _playerFactory = playerFactory;
        _settingsRepo = settingsRepo;
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
                        try { await prevTail; } catch { }
                        try
                        {
                            var result = await synthTask;
                            if (result != null)
                                await PlaySynthesisResultAsync(result, null);
                        }
                        catch (Exception ex)
                        {
                            _logger?.Error($"PreGenerated playback failed: {ex.Message}");
                        }
                    });
                    break;

                default:
                    await ProcessItem(item, null);
                    break;
            }
        }
    }

    private async Task<SynthesisResult?> SynthesizeItemAsync(TtsQueueItem item)
    {
        var result = await _synthesizer.SynthesizeAsync(
            item,
            () => ItemStarted?.Invoke(this, new QueueItemEventArgs { Item = item }));

        if (result == null)
            ItemCompleted?.Invoke(this, new QueueItemEventArgs { Item = item });

        return result;
    }

    private async Task PlaySynthesisResultAsync(SynthesisResult result, IAudioPlayer? playerOverride)
    {
        var settings = _settingsRepo.GetSettings();
        try
        {
            _logger?.Info($"QUEUE :: IsSilent={result.Item.IsSilent} DisableAudioOutput={settings.DisableAudioOutput}");
            if (!result.Item.IsSilent && !settings.DisableAudioOutput)
            {
                while (_paused && !_cts.IsCancellationRequested)
                    await Task.Delay(100);
                if (!_cts.IsCancellationRequested)
                    await _playback.PlayAsync(result.Item, result.Audio, result.DeviceId, settings.ApplicationVolume, playerOverride);
            }

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
    public void Stop() => _playback.Stop();
    public void StopUser(string userId) => _playback.StopUser(userId);
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
