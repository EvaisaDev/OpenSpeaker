using System.Collections.Concurrent;
using OpenSpeaker.Audio;
using OpenSpeaker.Models;
using OpenSpeaker.TTS;
namespace OpenSpeaker.Queue;

public class PlaybackCoordinator
{
    private readonly IAudioPlayer _sharedPlayer;
    private readonly ConcurrentDictionary<TtsQueueItem, IAudioPlayer> _playing = new();

    public PlaybackCoordinator(IAudioPlayer sharedPlayer)
    {
        _sharedPlayer = sharedPlayer;
    }

    public async Task PlayAsync(TtsQueueItem item, AudioData audio, string deviceId, int volume, IAudioPlayer? overridePlayer)
    {
        var player = overridePlayer ?? _sharedPlayer;
        _playing[item] = player;
        try
        {
            await player.PlayAsync(audio, deviceId, volume);
        }
        finally
        {
            _playing.TryRemove(item, out _);
        }
    }

    public void Stop()
    {
        _sharedPlayer.Stop();
        foreach (var player in _playing.Values)
            player.Stop();
    }

    public void StopUser(string userId)
    {
        foreach (var kvp in _playing)
            if (kvp.Key.UserId == userId)
                kvp.Value.Stop();
    }
}
