using OpenSpeaker.Data;
using OpenSpeaker.Models;
using OpenSpeaker.Queue;
using OpenSpeaker.Text;
namespace OpenSpeaker.Services;

public class TtsOrchestrator : ITtsOrchestrator
{
    private readonly ITtsQueue _queue;
    private readonly SettingsRepository _settingsRepo;
    private readonly MessageSanitizer _sanitizer;

    public bool IsEnabled => _settingsRepo.GetSettings().Enabled;

    public TtsOrchestrator(ITtsQueue queue, SettingsRepository settingsRepo, MessageSanitizer sanitizer)
    {
        _queue = queue;
        _settingsRepo = settingsRepo;
        _sanitizer = sanitizer;
    }

    public async Task<string?> SpeakAsync(string text, string voiceAliasName, bool applyBadWordFilter = true, bool silent = false, bool delay = false)
    {
        var settings = _settingsRepo.GetSettings();
        if (!settings.Enabled) return null;

        var sanitized = applyBadWordFilter ? _sanitizer.Sanitize(text, true) : text;
        if (string.IsNullOrWhiteSpace(sanitized)) return null;

        string? resultPath = null;
        var item = new TtsQueueItem
        {
            Text = sanitized,
            VoiceAliasName = voiceAliasName,
            ApplyBadWordFilter = false,
            IsSilent = silent
        };

        if (delay)
        {
            var tcs = new TaskCompletionSource<string?>();
            EventHandler<OpenSpeaker.Queue.QueueItemEventArgs>? handler = null;
            handler = (s, e) =>
            {
                if (e.Item == item)
                {
                    ((OpenSpeaker.Queue.TtsQueueService)_queue).ItemCompleted -= handler;
                    tcs.TrySetResult(e.OutputFilePath);
                }
            };
            ((OpenSpeaker.Queue.TtsQueueService)_queue).ItemCompleted += handler;
            _queue.Enqueue(item);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(Core.Constants.SpeakTimeoutSeconds));
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
            resultPath = completedTask == tcs.Task ? tcs.Task.Result : null;
        }
        else
        {
            _queue.Enqueue(item);
        }

        return resultPath;
    }

    public void Pause() => _queue.Pause();
    public void Resume() => _queue.Resume();
    public void Clear() => _queue.Clear();
    public void Stop() => _queue.Stop();

    public void SetEnabled(bool enabled)
    {
        var settings = _settingsRepo.GetSettings();
        settings.Enabled = enabled;
        _settingsRepo.SaveSettings(settings);
    }
}
