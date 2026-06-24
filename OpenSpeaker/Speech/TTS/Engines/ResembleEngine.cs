using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Infrastructure.Http;
using OpenSpeaker.Infrastructure.Logging;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public class ResembleEngine : HttpTtsEngine
{
    private static readonly IReadOnlyList<EngineParameterDef> Schema = new[]
    {
        EngineParameterDef.Combo("model", "Model",
            ["auto", "chatterbox-turbo", "chatterbox", "chatterbox-multilingual"],
            "auto"),
    };

    private readonly HttpClient _apiHttp = HttpClientFactory.GetClient("resemble-app", "https://app.resemble.ai");

    public ResembleEngine(IAppLogger? logger = null)
        : base("resemble", "https://f.cluster.resemble.ai", logger) { }

    public override string EngineId => EngineIds.Resemble;
    public override IReadOnlyList<EngineParameterDef> GetParameters() => Schema;

    protected override void ApplyAuth(HttpRequestMessage request) =>
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

    public override async Task<AudioData> SynthesizeAsync(string text, string voiceId, SynthParams parameters)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(text)) return AudioData.Empty;

        var model = parameters.Str("model", "auto");

        object body = string.Equals(model, "auto", StringComparison.OrdinalIgnoreCase)
            ? new { voice_uuid = voiceId, data = text, output_format = "mp3" }
            : new { voice_uuid = voiceId, data = text, model, output_format = "mp3" };

        var respText = await PostJsonForStringAsync("/synthesize", body);

        var b64 = JObject.Parse(respText)["audio_content"]?.Value<string>()
            ?? throw new InvalidOperationException($"Resemble TTS: no audio_content in response: {respText}");

        return await AudioDecoder.DecodeAsync(Convert.FromBase64String(b64));
    }

    public override async Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync()
    {
        if (!IsConfigured) return Array.Empty<VoiceInfo>();

        return await FetchAllPagesAsync(
            cursor => $"/api/v2/voices?page={cursor ?? "1"}&page_size=100",
            obj => obj["items"] as JArray ?? new JArray(),
            (obj, items, cursor) =>
            {
                var page = cursor == null ? 1 : int.Parse(cursor);
                return page < (obj["num_pages"]?.Value<int>() ?? 1) ? (page + 1).ToString() : null;
            },
            v =>
            {
                var id = v["uuid"]?.Value<string>() ?? string.Empty;
                if (string.IsNullOrEmpty(id)) return null;
                return new VoiceInfo { Id = id, Name = v["name"]?.Value<string>() ?? id };
            },
            client: _apiHttp);
    }
}
