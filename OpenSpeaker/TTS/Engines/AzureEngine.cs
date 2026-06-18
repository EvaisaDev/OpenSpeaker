using Microsoft.CognitiveServices.Speech;
using NAudio.Wave;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public class AzureEngine : ITtsEngine
{
    private static readonly IReadOnlyList<EngineParameterDef> Schema = new[]
    {
        EngineParameterDef.Slider("pitch", "Pitch (Hz)", -50, 50, 1, 0),
        EngineParameterDef.Slider("rate", "Rate (%)", -50, 200, 5, 0)
    };

    private string _subscriptionKey = string.Empty;
    private string _region = string.Empty;

    public string EngineId => EngineIds.Azure;
    public bool IsConfigured => !string.IsNullOrEmpty(_subscriptionKey) && !string.IsNullOrEmpty(_region);

    public IReadOnlyList<EngineParameterDef> GetParameters() => Schema;

    public void Configure(string configJson)
    {
        var obj = JObject.Parse(configJson);
        _subscriptionKey = obj["subscriptionKey"]?.Value<string>() ?? string.Empty;
        _region = obj["region"]?.Value<string>() ?? string.Empty;
    }

    public async Task<AudioData> SynthesizeAsync(string text, string voiceId, SynthParams parameters)
    {
        if (!IsConfigured) return AudioData.Empty;

        var pitch = parameters.Int("pitch", 0);
        var rate = parameters.Int("rate", 0);
        var config = SpeechConfig.FromSubscription(_subscriptionKey, _region);
        config.SpeechSynthesisVoiceName = voiceId;

        var pitchStr = pitch >= 0 ? $"+{pitch}Hz" : $"{pitch}Hz";
        var rateStr = rate >= 0 ? $"+{rate}%" : $"{rate}%";
        var ssml = $"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>" +
                   $"<voice name='{voiceId}'><prosody pitch='{pitchStr}' rate='{rateStr}'>" +
                   $"{System.Security.SecurityElement.Escape(text)}</prosody></voice></speak>";

        using var synthesizer = new SpeechSynthesizer(config, null);
        using var result = await synthesizer.SpeakSsmlAsync(ssml);

        if (result.Reason == ResultReason.SynthesizingAudioCompleted)
        {
            return new AudioData
            {
                Samples = result.AudioData,
                Format = new WaveFormat(16000, 16, 1)
            };
        }

        return AudioData.Empty;
    }

    public async Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync()
    {
        if (!IsConfigured) return Array.Empty<VoiceInfo>();

        var config = SpeechConfig.FromSubscription(_subscriptionKey, _region);
        using var synthesizer = new SpeechSynthesizer(config, null);
        using var result = await synthesizer.GetVoicesAsync();

        if (result.Reason == ResultReason.VoicesListRetrieved)
        {
            return result.Voices.Select(v => new VoiceInfo
            {
                Id = v.ShortName,
                Name = v.LocalName,
                Locale = v.Locale,
                Gender = v.Gender.ToString()
            }).ToList();
        }

        return Array.Empty<VoiceInfo>();
    }

    public void Dispose() { }
}
