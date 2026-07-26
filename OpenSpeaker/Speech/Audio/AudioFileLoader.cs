using System.IO;
using NAudio.Wave;
using OpenSpeaker.TTS;
namespace OpenSpeaker.Audio;

public static class AudioFileLoader
{
    public static async Task<AudioData> LoadAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return AudioData.Empty;
        try
        {
            using var fs = File.OpenRead(path);
            using var ms = new MemoryStream();
            await fs.CopyToAsync(ms);
            ms.Position = 0;
            using WaveStream reader = path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)
                ? new WaveFileReader(ms)
                : new Mp3FileReader(ms);
            using var pcm = WaveFormatConversionStream.CreatePcmStream(reader);
            using var outMs = new MemoryStream();
            await pcm.CopyToAsync(outMs);
            return new AudioData { Samples = outMs.ToArray(), Format = pcm.WaveFormat };
        }
        catch { return AudioData.Empty; }
    }
}
