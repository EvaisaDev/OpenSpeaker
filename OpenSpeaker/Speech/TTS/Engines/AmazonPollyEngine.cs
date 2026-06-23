using System.IO;
using Amazon;
using Amazon.Polly;
using Amazon.Polly.Model;
using NAudio.Wave;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public class AmazonPollyEngine : ITtsEngine
{
    private static readonly IReadOnlyList<string> EngineOptions = new[] { "standard", "neural", "long-form", "generative" };

    private static readonly IReadOnlyList<EngineParameterDef> Schema = new[]
    {
        EngineParameterDef.Combo("engine", "Engine", EngineOptions, "neural")
    };

    private AmazonPollyClient? _client;
    private string _accessKey = string.Empty;
    private string _secretKey = string.Empty;
    private string _region = string.Empty;

    public string EngineId => EngineIds.AmazonPolly;
    public bool IsConfigured => !string.IsNullOrEmpty(_accessKey) && !string.IsNullOrEmpty(_secretKey);

    public IReadOnlyList<EngineParameterDef> GetParameters() => Schema;

    public void Configure(string configJson)
    {
        var obj = JObject.Parse(configJson);
        _accessKey = obj["accessKey"]?.Value<string>() ?? string.Empty;
        _secretKey = obj["secretKey"]?.Value<string>() ?? string.Empty;
        _region = obj["region"]?.Value<string>() ?? "us-east-1";
        _client = new AmazonPollyClient(_accessKey, _secretKey, RegionEndpoint.GetBySystemName(_region));
    }

    public async Task<AudioData> SynthesizeAsync(string text, string voiceId, SynthParams parameters)
    {
        if (!IsConfigured || _client == null) return AudioData.Empty;

        var request = new SynthesizeSpeechRequest
        {
            VoiceId = voiceId,
            OutputFormat = OutputFormat.Pcm,
            SampleRate = "16000",
            Text = text,
            TextType = TextType.Text,
            Engine = parameters.Str("engine", "neural")
        };

        var response = await _client.SynthesizeSpeechAsync(request);
        using var ms = new MemoryStream();
        await response.AudioStream.CopyToAsync(ms);
        return new AudioData
        {
            Samples = ms.ToArray(),
            Format = new WaveFormat(16000, 16, 1)
        };
    }

    public async Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync()
    {
        if (!IsConfigured || _client == null) return Array.Empty<VoiceInfo>();

        var request = new DescribeVoicesRequest { Engine = Engine.Neural };
        var response = await _client.DescribeVoicesAsync(request);

        return response.Voices.Select(v => new VoiceInfo
        {
            Id = v.Id.Value,
            Name = v.Name,
            Locale = v.LanguageCode.Value,
            Gender = v.Gender.Value
        }).ToList();
    }

    public void Dispose() => _client?.Dispose();
}
