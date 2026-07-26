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
    IReadOnlyList<TtsQueueItem> GetPlayingItems();
    IReadOnlyList<TtsQueueItem> GetQueuedItems();
    bool StopId(string speechId);
    bool RemoveId(string speechId);
    event EventHandler<QueueItemEventArgs> ItemQueued;
    event EventHandler<QueueItemEventArgs> ItemStarted;
    event EventHandler<QueueItemEventArgs> ItemSynthesized;
    event EventHandler<QueueItemEventArgs> ItemPlaying;
    event EventHandler<QueueItemEventArgs> ItemCompleted;
}
