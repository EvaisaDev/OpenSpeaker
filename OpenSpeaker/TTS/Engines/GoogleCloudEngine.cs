using System.IO;
using Google.Cloud.TextToSpeech.V1;
using Grpc.Core;
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
    private readonly HashSet<string> _noPitchVoices = new(StringComparer.OrdinalIgnoreCase);

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

        var pitch = parameters.Dbl("pitch", 0);
        var supportsPitch = !_noPitchVoices.Contains(voiceName);

        try
        {
            return await SynthesizeInternalAsync(voiceName, languageCode, text, parameters, supportsPitch && pitch != 0 ? pitch : null);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.InvalidArgument && ex.Status.Detail.Contains("pitch"))
        {
            _noPitchVoices.Add(voiceName);
            return await SynthesizeInternalAsync(voiceName, languageCode, text, parameters, null);
        }
    }

    private async Task<AudioData> SynthesizeInternalAsync(string voiceName, string languageCode, string text, SynthParams parameters, double? pitch)
    {
        var audioConfig = new AudioConfig
        {
            AudioEncoding = AudioEncoding.Linear16,
            SpeakingRate = parameters.Dbl("speaking_rate", 1.0),
            VolumeGainDb = parameters.Dbl("volume_gain_db", 0)
        };
        if (pitch.HasValue) audioConfig.Pitch = pitch.Value;

        var request = new SynthesizeSpeechRequest
        {
            Input = new SynthesisInput { Text = text },
            Voice = new VoiceSelectionParams { Name = voiceName, LanguageCode = languageCode },
            AudioConfig = audioConfig
        };

        var response = await _client!.SynthesizeSpeechAsync(request);
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
