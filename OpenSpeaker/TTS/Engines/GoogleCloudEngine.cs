using System.IO;
using Google.Cloud.TextToSpeech.V1;
using NAudio.Wave;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public class GoogleCloudEngine : ITtsEngine
{
    private static readonly IReadOnlyList<EngineParameterDef> Schema = new[]
    {
        EngineParameterDef.Slider("pitch", "Pitch (st)", -20, 20, 0.5, 0),
        EngineParameterDef.Slider("speaking_rate", "Rate", 0.25, 4.0, 0.05, 1.0),
        EngineParameterDef.Slider("volume_gain_db", "Volume (dB)", -16, 16, 0.5, 0)
    };

    private TextToSpeechClient? _client;
    private string _serviceAccountPath = string.Empty;

    public string EngineId => EngineIds.GoogleCloud;
    public bool IsConfigured => !string.IsNullOrEmpty(_serviceAccountPath) && File.Exists(_serviceAccountPath);

    public IReadOnlyList<EngineParameterDef> GetParameters() => Schema;

    public void Configure(string configJson)
    {
        var obj = JObject.Parse(configJson);
        _serviceAccountPath = obj["serviceAccountJsonPath"]?.ToString() ?? string.Empty;
        if (IsConfigured)
            _client = new TextToSpeechClientBuilder { CredentialsPath = _serviceAccountPath }.Build();
    }

    public async Task<AudioData> SynthesizeAsync(string text, string voiceId, SynthParams parameters)
    {
        if (!IsConfigured || _client == null) return AudioData.Empty;

        var parts = voiceId.Split('|');
        var voiceName = parts[0];
        var languageCode = parts.Length > 1 ? parts[1] : "en-US";

        var request = new SynthesizeSpeechRequest
        {
            Input = new SynthesisInput { Text = text },
            Voice = new VoiceSelectionParams { Name = voiceName, LanguageCode = languageCode },
            AudioConfig = new AudioConfig
            {
                AudioEncoding = AudioEncoding.Linear16,
                SpeakingRate = parameters.Dbl("speaking_rate", 1.0),
                Pitch = parameters.Dbl("pitch", 0),
                VolumeGainDb = parameters.Dbl("volume_gain_db", 0)
            }
        };

        var response = await _client.SynthesizeSpeechAsync(request);
        return new AudioData
        {
            Samples = response.AudioContent.ToByteArray(),
            Format = new WaveFormat(24000, 16, 1)
        };
    }

    public async Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync()
    {
        if (!IsConfigured || _client == null) return Array.Empty<VoiceInfo>();

        var response = await _client.ListVoicesAsync(new ListVoicesRequest());
        return response.Voices.Select(v => new VoiceInfo
        {
            Id = $"{v.Name}|{v.LanguageCodes.FirstOrDefault() ?? "en-US"}",
            Name = v.Name,
            Locale = v.LanguageCodes.FirstOrDefault() ?? string.Empty,
            Gender = v.SsmlGender.ToString()
        }).ToList();
    }

    public void Dispose() { }
}
