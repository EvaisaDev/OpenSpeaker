using System.Speech.Synthesis;
using System.IO;
using NAudio.Wave;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public class Sapi5Engine : ITtsEngine
{
    private static readonly IReadOnlyList<EngineParameterDef> Schema = new[]
    {
        EngineParameterDef.Slider("rate", "Rate", -10, 10, 1, 0),
        EngineParameterDef.Slider("pitch", "Pitch (Hz)", -200, 200, 10, 0)
    };

    private readonly SpeechSynthesizer _synth = new();
    public string EngineId => EngineIds.Sapi5;
    public bool IsConfigured => true;

    public IReadOnlyList<EngineParameterDef> GetParameters() => Schema;

    public async Task<AudioData> SynthesizeAsync(string text, string voiceId, SynthParams parameters)
    {
        return await Task.Run(() =>
        {
            if (!string.IsNullOrEmpty(voiceId))
            {
                try { _synth.SelectVoice(voiceId); } catch { }
            }

            _synth.Volume = 100;
            _synth.Rate = Math.Clamp(parameters.Int("rate", 0), -10, 10);
            var pitch = parameters.Int("pitch", 0);
            var ssml = BuildSsml(text, pitch);
            using var stream = new MemoryStream();
            _synth.SetOutputToWaveStream(stream);
            _synth.SpeakSsml(ssml);
            _synth.SetOutputToNull();
            return new AudioData
            {
                Samples = stream.ToArray(),
                Format = new WaveFormat(22050, 16, 1)
            };
        });
    }

    private static string BuildSsml(string text, int pitch)
    {
        var contour = pitch != 0
            ? $"<prosody pitch=\"{(pitch > 0 ? "+" : "")}{pitch}Hz\">{System.Security.SecurityElement.Escape(text)}</prosody>"
            : System.Security.SecurityElement.Escape(text);
        return $"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\">{contour}</speak>";
    }

    public async Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync()
    {
        return await Task.Run(() =>
            _synth.GetInstalledVoices()
                .Where(v => v.Enabled)
                .Select(v => new VoiceInfo
                {
                    Id = v.VoiceInfo.Name,
                    Name = v.VoiceInfo.Name,
                    Locale = v.VoiceInfo.Culture.Name,
                    Gender = v.VoiceInfo.Gender.ToString()
                })
                .ToList()
        );
    }

    public void Configure(string configJson) { }
    public void Dispose() => _synth.Dispose();
}
