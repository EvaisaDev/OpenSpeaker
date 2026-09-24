using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OpenSpeaker.TTS;
namespace OpenSpeaker.Audio;

public static class AudioMerger
{
	public static AudioData Concat(IReadOnlyList<AudioData> clips)
	{
		var parts = clips.Where(c => !c.IsEmpty).ToList();
		if (parts.Count == 0) return AudioData.Empty;
		if (parts.Count == 1) return parts[0];

		var first = parts[0].Format;
		if (parts.All(p => SameFormat(p.Format, first)))
			return new AudioData { Samples = Join(parts.Select(p => p.Samples)), Format = first };

		var target = new WaveFormat(
			parts.Max(p => p.Format.SampleRate),
			16,
			parts.Any(p => p.Format.Channels > 1) ? 2 : 1);

		return new AudioData { Samples = Join(parts.Select(p => Convert(p, target))), Format = target };
	}

	private static bool SameFormat(WaveFormat a, WaveFormat b) =>
		a.Encoding == b.Encoding
		&& a.SampleRate == b.SampleRate
		&& a.Channels == b.Channels
		&& a.BitsPerSample == b.BitsPerSample;

	private static byte[] Join(IEnumerable<byte[]> chunks)
	{
		using var ms = new MemoryStream();
		foreach (var c in chunks) ms.Write(c, 0, c.Length);
		return ms.ToArray();
	}

	private static byte[] Convert(AudioData audio, WaveFormat target)
	{
		if (SameFormat(audio.Format, target)) return audio.Samples;

		using var source = new RawSourceWaveStream(new MemoryStream(audio.Samples), audio.Format);
		ISampleProvider provider = source.ToSampleProvider();

		if (provider.WaveFormat.Channels > 2)
			provider = new MultiplexingSampleProvider(new[] { provider }, 2);
		if (provider.WaveFormat.Channels == 1 && target.Channels == 2)
			provider = new MonoToStereoSampleProvider(provider);
		else if (provider.WaveFormat.Channels == 2 && target.Channels == 1)
			provider = new StereoToMonoSampleProvider(provider);

		if (provider.WaveFormat.SampleRate != target.SampleRate)
			provider = new WdlResamplingSampleProvider(provider, target.SampleRate);

		var wave16 = new SampleToWaveProvider16(provider);
		using var ms = new MemoryStream();
		var buffer = new byte[wave16.WaveFormat.AverageBytesPerSecond];
		int read;
		while ((read = wave16.Read(buffer, 0, buffer.Length)) > 0)
			ms.Write(buffer, 0, read);
		return ms.ToArray();
	}
}
