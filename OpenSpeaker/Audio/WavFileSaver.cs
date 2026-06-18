using System.IO;
using NAudio.Wave;
using OpenSpeaker.TTS;
namespace OpenSpeaker.Audio;

public class WavFileSaver
{
    public string Save(AudioData audio, string folder)
    {
        if (audio.IsEmpty) return string.Empty;
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

        var fileName = $"tts_{DateTime.Now:yyyyMMdd_HHmmss_fff}.wav";
        var path = Path.Combine(folder, fileName);

        using var ms = new MemoryStream(audio.Samples);
        using var reader = new RawSourceWaveStream(ms, audio.Format);
        WaveFileWriter.CreateWaveFile(path, reader);

        return path;
    }
}
