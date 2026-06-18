using System.IO;
using System.Net.Http;
using System.Text;
using NAudio.Wave;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public class CustomApiEngine : ITtsEngine
{
    private readonly CustomApiDefinition _def;
    private readonly HttpClient _http = new();

    public string EngineId => _def.EngineId;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_def.SynthUrl);

    public CustomApiEngine(CustomApiDefinition def) => _def = def;

    public void Configure(string configJson) { }

    public IReadOnlyList<EngineParameterDef> GetParameters() => Array.Empty<EngineParameterDef>();

    public async Task<AudioData> SynthesizeAsync(string text, string voiceId, SynthParams parameters)
    {
        if (!IsConfigured) return AudioData.Empty;
        try
        {
            var url = _def.SynthUrl
                .Replace("{text}", Uri.EscapeDataString(text))
                .Replace("{voice}", Uri.EscapeDataString(voiceId));

            var method = _def.SynthMethod.ToUpperInvariant() == "GET" ? HttpMethod.Get : HttpMethod.Post;
            var request = new HttpRequestMessage(method, url);

            foreach (var h in _def.SynthHeaders)
                if (!string.IsNullOrEmpty(h.Key))
                    request.Headers.TryAddWithoutValidation(h.Key, h.Value);

            if (method == HttpMethod.Post && !string.IsNullOrEmpty(_def.SynthBodyTemplate))
            {
                var body = _def.SynthBodyTemplate
                    .Replace("{text}", JsonEscape(text))
                    .Replace("{voice}", JsonEscape(voiceId));
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return AudioData.Empty;

            byte[] audioBytes;
            switch (_def.ResponseFormat.ToLowerInvariant())
            {
                case "base64":
                    var jsonB64 = await response.Content.ReadAsStringAsync();
                    var b64Token = Select(JToken.Parse(jsonB64), _def.ResponseAudioPath);
                    audioBytes = Convert.FromBase64String(b64Token?.Value<string>() ?? "");
                    break;
                case "url":
                    var jsonUrl = await response.Content.ReadAsStringAsync();
                    var urlToken = Select(JToken.Parse(jsonUrl), _def.ResponseAudioPath);
                    audioBytes = await _http.GetByteArrayAsync(urlToken?.Value<string>() ?? "");
                    break;
                default:
                    audioBytes = await response.Content.ReadAsByteArrayAsync();
                    break;
            }

            return await ToAudioDataAsync(audioBytes, _def.AudioFormat);
        }
        catch { return AudioData.Empty; }
    }

    public async Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync()
    {
        if (string.IsNullOrWhiteSpace(_def.VoicesUrl)) return Array.Empty<VoiceInfo>();
        try
        {
            var method = _def.VoicesMethod.ToUpperInvariant() == "POST" ? HttpMethod.Post : HttpMethod.Get;
            var request = new HttpRequestMessage(method, _def.VoicesUrl);
            foreach (var h in _def.VoicesHeaders)
                if (!string.IsNullOrEmpty(h.Key))
                    request.Headers.TryAddWithoutValidation(h.Key, h.Value);

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return Array.Empty<VoiceInfo>();

            var json = await response.Content.ReadAsStringAsync();
            var root = JToken.Parse(json);

            IEnumerable<JToken> items;
            if (string.IsNullOrEmpty(_def.VoicesArrayPath))
            {
                items = root as JArray ?? new JArray(root);
            }
            else
            {
                var selected = root.SelectTokens(_def.VoicesArrayPath);
                items = selected.SelectMany(t => t is JArray a ? a.AsEnumerable() : new[] { t });
            }

            return items
                .Select(v => new VoiceInfo
                {
                    Id = Select(v, _def.VoiceIdField)?.Value<string>() ?? string.Empty,
                    Name = Select(v, _def.VoiceNameField)?.Value<string>() ?? string.Empty
                })
                .Where(v => !string.IsNullOrEmpty(v.Id))
                .ToList();
        }
        catch { return Array.Empty<VoiceInfo>(); }
    }

    private static JToken? Select(JToken root, string? path) =>
        string.IsNullOrEmpty(path) ? root : root.SelectToken(path);

    private static async Task<AudioData> ToAudioDataAsync(byte[] bytes, string format)
    {
        if (bytes.Length == 0) return AudioData.Empty;
        try
        {
            using var ms = new MemoryStream(bytes);
            WaveStream reader = format.ToLowerInvariant() == "wav"
                ? (WaveStream)new WaveFileReader(ms)
                : new Mp3FileReader(ms);
            using var pcm = WaveFormatConversionStream.CreatePcmStream(reader);
            using var outMs = new MemoryStream();
            await pcm.CopyToAsync(outMs);
            return new AudioData { Samples = outMs.ToArray(), Format = pcm.WaveFormat };
        }
        catch { return AudioData.Empty; }
    }

    private static string JsonEscape(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

    public void Dispose() => _http.Dispose();
}
