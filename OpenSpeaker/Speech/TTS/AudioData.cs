using System.IO;
using NAudio.Wave;
namespace OpenSpeaker.TTS;
public class AudioData
{
    public byte[] Samples { get; init; } = Array.Empty<byte>();
    public WaveFormat Format { get; init; } = new WaveFormat(44100, 16, 1);
    public TimeSpan Duration => TimeSpan.FromSeconds((double)Samples.Length / (Format.SampleRate * Format.Channels * (Format.BitsPerSample / 8)));

    public static AudioData Empty => new();
    public bool IsEmpty => Samples.Length == 0;

    public byte[] ToWavBytes()
    {
        var byteRate = Format.SampleRate * Format.Channels * (Format.BitsPerSample / 8);
        var blockAlign = (short)(Format.Channels * (Format.BitsPerSample / 8));

        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + Samples.Length);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1);
            bw.Write((short)Format.Channels);
            bw.Write(Format.SampleRate);
            bw.Write(byteRate);
            bw.Write(blockAlign);
            bw.Write((short)Format.BitsPerSample);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(Samples.Length);
            bw.Write(Samples);
        }
        return ms.ToArray();
    }

    public string ToWavBase64() => Convert.ToBase64String(ToWavBytes());
}
