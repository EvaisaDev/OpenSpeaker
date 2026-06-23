using System.IO;
using NAudio.Wave;
namespace OpenSpeaker.TTS;

public static class AudioDecoder
{
    public static async Task<AudioData> DecodeAsync(byte[] bytes, string format = "mp3")
    {
        if (bytes == null || bytes.Length == 0) return AudioData.Empty;

        using var ms = new MemoryStream(bytes);
        WaveStream reader = format.ToLowerInvariant() == "wav"
            ? (WaveStream)new WaveFileReader(ms)
            : new Mp3FileReader(ms);
        using (reader)
        using (var pcmStream = WaveFormatConversionStream.CreatePcmStream(reader))
        using (var pcmMs = new MemoryStream())
        {
            await pcmStream.CopyToAsync(pcmMs);
            return new AudioData { Samples = pcmMs.ToArray(), Format = pcmStream.WaveFormat };
        }
    }
}
