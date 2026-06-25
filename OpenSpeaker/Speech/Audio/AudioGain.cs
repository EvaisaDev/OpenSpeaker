using OpenSpeaker.TTS;
namespace OpenSpeaker.Audio;

public static class AudioGain
{
    public static AudioData Apply(AudioData audio, int volumePercent)
    {
        if (audio.IsEmpty || volumePercent == 100) return audio;
        if (audio.Format.BitsPerSample != 16) return audio;

        var gain = volumePercent / 100f;
        var src  = audio.Samples;
        var dst  = new byte[src.Length];
        for (var i = 0; i + 1 < src.Length; i += 2)
        {
            var sample  = (short)(src[i] | (src[i + 1] << 8));
            var clamped = (short)Math.Clamp(sample * gain, short.MinValue, short.MaxValue);
            dst[i]     = (byte)(clamped & 0xFF);
            dst[i + 1] = (byte)((clamped >> 8) & 0xFF);
        }
        return new AudioData { Samples = dst, Format = audio.Format };
    }
}
