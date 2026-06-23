using OpenSpeaker.Models;
namespace OpenSpeaker.Queue;
public interface ITtsQueue
{
    bool IsPaused { get; }
    int Count { get; }
    (string VoiceId, string EngineId) LastUsedVoice { get; }
    void Enqueue(TtsQueueItem item);
    void Pause();
    void Resume();
    void Clear();
    void Stop();
    void StopUser(string userId);
    void SkipUser(string userId);
    event EventHandler<QueueItemEventArgs> ItemStarted;
    event EventHandler<QueueItemEventArgs> ItemCompleted;
}
