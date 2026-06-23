using OpenSpeaker.TTS;
namespace OpenSpeaker.Audio;
public interface IAudioPlayer : IDisposable
{
    Task PlayAsync(AudioData audio, string deviceId, int volume);
    void Stop();
}
