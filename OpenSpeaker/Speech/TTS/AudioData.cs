using NAudio.Wave;
namespace OpenSpeaker.TTS;
public class AudioData
{
    public byte[] Samples { get; init; } = Array.Empty<byte>();
    public WaveFormat Format { get; init; } = new WaveFormat(44100, 16, 1);
    public TimeSpan Duration => TimeSpan.FromSeconds((double)Samples.Length / (Format.SampleRate * Format.Channels * (Format.BitsPerSample / 8)));

    public static AudioData Empty => new();
    public bool IsEmpty => Samples.Length == 0;
}
