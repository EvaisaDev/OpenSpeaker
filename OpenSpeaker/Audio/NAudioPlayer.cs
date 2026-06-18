using System.IO;
using NAudio.Wave;
using OpenSpeaker.TTS;
namespace OpenSpeaker.Audio;

public class NAudioPlayer : IAudioPlayer
{
    private WaveOutEvent? _currentOutput;
    private readonly object _lock = new();

    public async Task PlayAsync(AudioData audio, string deviceId, int volume)
    {
        if (audio.IsEmpty) return;

        Stop();

        var output = new WaveOutEvent();

        if (int.TryParse(deviceId, out var deviceNumber))
            output.DeviceNumber = deviceNumber;

        output.Volume = Math.Clamp(volume / 100f, 0f, 1f);

        lock (_lock)
        {
            _currentOutput = output;
        }

        var tcs = new TaskCompletionSource<bool>();
        output.PlaybackStopped += (_, _) => tcs.TrySetResult(true);

        using var ms = new MemoryStream(audio.Samples);
        using var provider = new RawSourceWaveStream(ms, audio.Format);
        output.Init(provider);
        output.Play();

        await tcs.Task;

        lock (_lock)
        {
            if (ReferenceEquals(_currentOutput, output))
                _currentOutput = null;
        }

        output.Dispose();
    }

    public void Stop()
    {
        lock (_lock)
        {
            _currentOutput?.Stop();
        }
    }

    public void Dispose()
    {
        Stop();
        lock (_lock)
        {
            _currentOutput?.Dispose();
            _currentOutput = null;
        }
    }
}
