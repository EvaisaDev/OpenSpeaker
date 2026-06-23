using System.Speech.Synthesis;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using NAudio.Wave;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public class Sapi5Engine : ITtsEngine
{
    private static readonly IReadOnlyList<EngineParameterDef> Schema = new[]
    {
        EngineParameterDef.Slider("rate", "Rate", -10, 10, 1, 0),
        EngineParameterDef.Slider("pitch", "Pitch", -2, 2, 1, 0)
    };

    private const int MaxPooledSynthesizers = 4;
    private readonly ConcurrentBag<SpeechSynthesizer> _pool = new();
    private int _idleCount;

    public string EngineId => EngineIds.Sapi5;
    public bool IsConfigured => true;

    public IReadOnlyList<EngineParameterDef> GetParameters() => Schema;

    public static string GetDefaultVoiceName()
    {
        try
        {
            using var synth = new SpeechSynthesizer();
            return synth.Voice.Name;
        }
        catch { return string.Empty; }
    }

    private SpeechSynthesizer Rent()
    {
        if (_pool.TryTake(out var synth))
        {
            Interlocked.Decrement(ref _idleCount);
            return synth;
        }
        return new SpeechSynthesizer();
    }

    private void Return(SpeechSynthesizer synth)
    {
        if (Interlocked.Increment(ref _idleCount) <= MaxPooledSynthesizers)
        {
            _pool.Add(synth);
        }
        else
        {
            Interlocked.Decrement(ref _idleCount);
            synth.Dispose();
        }
    }

    public async Task<AudioData> SynthesizeAsync(string text, string voiceId, SynthParams parameters)
    {
        return await Task.Run(() =>
        {
            var synth = Rent();
            try
            {
                if (!string.IsNullOrEmpty(voiceId))
                {
                    try { synth.SelectVoice(voiceId); } catch { }
                }

                synth.Volume = 100;
                synth.Rate = Math.Clamp(parameters.Int("rate", 0), -10, 10);
                var pitch = parameters.Int("pitch", 0);
                var ssml = BuildSsml(text, pitch);
                using var stream = new MemoryStream();
                synth.SetOutputToWaveStream(stream);
                synth.SpeakSsml(ssml);
                synth.SetOutputToNull();
                return new AudioData
                {
                    Samples = stream.ToArray(),
                    Format = new WaveFormat(22050, 16, 1)
                };
            }
            finally
            {
                Return(synth);
            }
        });
    }

    private static readonly Regex EmphasisPattern = new(@"\*(\S+)", RegexOptions.Compiled);

    private static string BuildInnerXml(string text)
    {
        var sb = new StringBuilder();
        var lastIdx = 0;
        foreach (Match m in EmphasisPattern.Matches(text))
        {
            sb.Append(System.Security.SecurityElement.Escape(text[lastIdx..m.Index]));
            sb.Append($"<emphasis level=\"strong\">{System.Security.SecurityElement.Escape(m.Groups[1].Value)}</emphasis>");
            lastIdx = m.Index + m.Length;
        }
        sb.Append(System.Security.SecurityElement.Escape(text[lastIdx..]));
        return sb.ToString();
    }

    private static readonly string[] PitchNames = ["x-low", "low", "medium", "high", "x-high"];

    private static string BuildSsml(string text, int pitch)
    {
        var inner = BuildInnerXml(text);
        var pitchName = PitchNames[Math.Clamp(pitch + 2, 0, 4)];
        var body = pitch != 0
            ? $"<prosody pitch='{pitchName}'>{inner}</prosody>"
            : inner;
        return $"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\">{body}</speak>";
    }

    public async Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync()
    {
        return await Task.Run(() =>
        {
            var synth = Rent();
            try
            {
                return synth.GetInstalledVoices()
                    .Where(v => v.Enabled)
                    .Select(v => new VoiceInfo
                    {
                        Id = v.VoiceInfo.Name,
                        Name = v.VoiceInfo.Name,
                        Locale = v.VoiceInfo.Culture.Name,
                        Gender = v.VoiceInfo.Gender.ToString()
                    })
                    .ToList();
            }
            finally
            {
                Return(synth);
            }
        });
    }

    public void Configure(string configJson) { }

    public void Dispose()
    {
        while (_pool.TryTake(out var synth))
            synth.Dispose();
    }
}
