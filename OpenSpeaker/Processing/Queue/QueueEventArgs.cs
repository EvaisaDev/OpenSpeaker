using OpenSpeaker.Models;
namespace OpenSpeaker.Queue;
public class QueueItemEventArgs : EventArgs
{
    public TtsQueueItem Item { get; init; } = new();
    public string? OutputFilePath { get; init; }
    public TimeSpan Duration { get; init; }
}
