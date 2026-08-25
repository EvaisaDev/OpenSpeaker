using OpenSpeaker.Audio;
using OpenSpeaker.Data;
using OpenSpeaker.Extensions;
using OpenSpeaker.Infrastructure.Logging;
using OpenSpeaker.Models;
using OpenSpeaker.TTS;
using OpenSpeaker.Users;
using System.Collections.Concurrent;
using System.IO;
namespace OpenSpeaker.Queue;

public class TtsQueueService : ITtsQueue, IDisposable
{
    private readonly BlockingCollection<TtsQueueItem> _queue = new();
    private readonly TtsSynthesizer _synthesizer;
    private readonly PlaybackCoordinator _playback;
    private readonly Func<IAudioPlayer> _playerFactory;
    private readonly SettingsRepository _settingsRepo;
    private readonly ExtensionManager? _extensions;
    private readonly CancellationTokenSource _cts = new();
    private bool _paused = false;
    private readonly object _pauseLock = new();
    private readonly IAppLogger? _logger;
    private Task _pregenTail = Task.CompletedTask;
    private CancellationTokenSource _clearCts = new();

    public event EventHandler<QueueItemEventArgs>? ItemQueued;
    public event EventHandler<QueueItemEventArgs>? ItemStarted;
    public event EventHandler<QueueItemEventArgs>? ItemSynthesized;
    public event EventHandler<QueueItemEventArgs>? ItemPlaying;
    public event EventHandler<QueueItemEventArgs>? ItemCompleted;

    public bool IsPaused => _paused;
    public int Count => _queue.Count;
    public (string VoiceId, string EngineId) LastUsedVoice => _synthesizer.LastUsedVoice;

    public TtsQueueService(
        TtsSynthesizer synthesizer,
        PlaybackCoordinator playback,
        Func<IAudioPlayer> playerFactory,
        SettingsRepository settingsRepo,
        ExtensionManager? extensions = null,
        IAppLogger? logger = null)
    {
        _synthesizer = synthesizer;
        _playback = playback;
        _playerFactory = playerFactory;
        _settingsRepo = settingsRepo;
        _extensions = extensions;
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

            var clearToken = _clearCts.Token;
            var settings = _settingsRepo.GetSettings();
            switch (settings.QueueMode)
            {
                case QueueModes.Simultaneous:
                    _ = Task.Run(() => ProcessItem(item, _playerFactory(), clearToken));
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
                                await PlaySynthesisResultAsync(result, null, clearToken);
                        }
                        catch (Exception ex)
                        {
                            _logger?.Error($"PreGenerated playback failed: {ex.Message}");
                        }
                    });
                    break;

                default:
                    await ProcessItem(item, null, clearToken);
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
        {
            ItemCompleted?.Invoke(this, new QueueItemEventArgs { Item = item });
        }
        else
        {
            ItemSynthesized?.Invoke(this, new QueueItemEventArgs
            {
                Item           = item,
                OutputFilePath = result.SavedPath,
                Duration       = result.Audio.Duration,
            });
        }

        return result;
    }

    private async Task PlaySynthesisResultAsync(SynthesisResult result, IAudioPlayer? playerOverride, CancellationToken clearToken = default)
    {
        var settings = _settingsRepo.GetSettings();
        try
        {
            var suppressPlayback = result.Item.IsSilent || clearToken.IsCancellationRequested;
            if (!suppressPlayback && _extensions is { HasBeforeSpeakHooks: true } && !result.Audio.IsEmpty)
            {
                var wavBase64 = ToWavBase64(result.Audio);
                var action = await _extensions.InvokeBeforeSpeakAsync(result.Item.UserId, result.Item.Username, wavBase64);
                if (string.Equals(action, "mute", StringComparison.OrdinalIgnoreCase))
                    suppressPlayback = true;
            }

            _logger?.Info($"QUEUE :: IsSilent={result.Item.IsSilent} DisableAudioOutput={settings.DisableAudioOutput}");
            if (!suppressPlayback && !settings.DisableAudioOutput)
            {
                while (_paused && !_cts.IsCancellationRequested)
                    await Task.Delay(100);
                if (!_cts.IsCancellationRequested && !clearToken.IsCancellationRequested)
                {
                    ItemPlaying?.Invoke(this, new QueueItemEventArgs
                    {
                        Item           = result.Item,
                        OutputFilePath = result.SavedPath,
                        Duration       = result.Audio.Duration,
                    });
                    await _playback.PlayAsync(result.Item, result.Audio, result.DeviceId, settings.ApplicationVolume, playerOverride);
                }
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

    private async Task ProcessItem(TtsQueueItem item, IAudioPlayer? playerOverride, CancellationToken clearToken = default)
    {
        var result = await SynthesizeItemAsync(item);
        if (result != null)
            await PlaySynthesisResultAsync(result, playerOverride, clearToken);
        else
            playerOverride?.Dispose();
    }

    public void Enqueue(TtsQueueItem item)
    {
        _queue.TryAdd(item);
        ItemQueued?.Invoke(this, new QueueItemEventArgs { Item = item });
    }
    public void Pause() { lock (_pauseLock) { _paused = true; } }
    public void Resume() { lock (_pauseLock) { _paused = false; } }
    public void Clear()
    {
        while (_queue.TryTake(out _)) { }
        var oldCts = Interlocked.Exchange(ref _clearCts, new CancellationTokenSource());
        oldCts.Cancel();
        oldCts.Dispose();
        _playback.Stop();
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

    public IReadOnlyList<TtsQueueItem> GetPlayingItems() => _playback.GetPlayingItems();
    public IReadOnlyList<TtsQueueItem> GetQueuedItems() => _queue.ToArray();
    public bool StopId(string speechId) => _playback.StopId(speechId);
    public bool RemoveId(string speechId)
    {
        var kept = new List<TtsQueueItem>();
        var removed = false;
        while (_queue.TryTake(out var item))
        {
            if (item.SpeechId == speechId) removed = true;
            else kept.Add(item);
        }
        foreach (var item in kept) _queue.TryAdd(item);
        return removed;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _queue.CompleteAdding();
        _cts.Dispose();
        _queue.Dispose();
        _clearCts.Dispose();
    }

    private static string ToWavBase64(AudioData audio)
    {
        var format = audio.Format;
        var samples = audio.Samples;
        var byteRate = format.SampleRate * format.Channels * (format.BitsPerSample / 8);
        var blockAlign = (short)(format.Channels * (format.BitsPerSample / 8));

        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + samples.Length);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1);
            bw.Write((short)format.Channels);
            bw.Write(format.SampleRate);
            bw.Write(byteRate);
            bw.Write(blockAlign);
            bw.Write((short)format.BitsPerSample);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(samples.Length);
            bw.Write(samples);
        }
        return Convert.ToBase64String(ms.ToArray());
    }
}
