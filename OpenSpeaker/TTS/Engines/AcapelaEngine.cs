using System.IO;
using System.Net.Http;
using NAudio.Wave;
using Newtonsoft.Json.Linq;
using OpenSpeaker.ThingsIDKWhereToPut.Http;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public class AcapelaEngine : ITtsEngine
{
    private static readonly IReadOnlyList<EngineParameterDef> Schema = new[]
    {
        EngineParameterDef.Slider("rate", "Rate", 60, 240, 10, 100),
        EngineParameterDef.Slider("pitch", "Pitch", 0, 200, 10, 100)
    };

    private string _account = string.Empty;
    private string _password = string.Empty;
    private string _appName = string.Empty;
    private readonly HttpClient _http = HttpClientFactory.GetClient("acapela", "https://vaas.acapela-cloud.com");

    public string EngineId => EngineIds.Acapela;
    public bool IsConfigured => !string.IsNullOrEmpty(_account) && !string.IsNullOrEmpty(_password);

    public IReadOnlyList<EngineParameterDef> GetParameters() => Schema;

    public void Configure(string configJson)
    {
        var obj = JObject.Parse(configJson);
        _account = obj["account"]?.Value<string>() ?? string.Empty;
        _password = obj["password"]?.Value<string>() ?? string.Empty;
        _appName = obj["applicationName"]?.Value<string>() ?? "OpenSpeaker";
    }

    public async Task<AudioData> SynthesizeAsync(string text, string voiceId, SynthParams parameters)
    {
        if (!IsConfigured) return AudioData.Empty;

        var formData = new Dictionary<string, string>
        {
            ["req_loginID"] = _account,
            ["req_passwd"] = _password,
            ["req_vers"] = "2",
            ["req_snd_id"] = _appName,
            ["req_voice"] = voiceId,
            ["req_text"] = text,
            ["req_spd"] = parameters.Int("rate", 100).ToString(),
            ["req_vct"] = parameters.Int("pitch", 100).ToString(),
            ["req_vol"] = "100",
            ["req_snd_type"] = "MP3"
        };

        try
        {
            var response = await _http.PostAsync("/Services/Synthesizer", new FormUrlEncodedContent(formData));
            if (!response.IsSuccessStatusCode) return AudioData.Empty;

            var json = await response.Content.ReadAsStringAsync();
            var obj2 = JObject.Parse(json);
            var sndUrl = obj2["snd_url"]?.Value<string>() ?? string.Empty;
            if (string.IsNullOrEmpty(sndUrl)) return AudioData.Empty;

            var mp3Bytes = await _http.GetByteArrayAsync(sndUrl);
            using var ms = new MemoryStream(mp3Bytes);
            using var reader = new Mp3FileReader(ms);
            using var pcmStream = WaveFormatConversionStream.CreatePcmStream(reader);
            using var pcmMs = new MemoryStream();
            await pcmStream.CopyToAsync(pcmMs);
            return new AudioData { Samples = pcmMs.ToArray(), Format = pcmStream.WaveFormat };
        }
        catch
        {
            return AudioData.Empty;
        }
    }

    public async Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync() =>
        await Task.FromResult<IReadOnlyList<VoiceInfo>>(Array.Empty<VoiceInfo>());

    public void Dispose() { }
}
