using System.IO;
using System.Net.Http;
using System.Text;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Infrastructure.Http;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public class TikTokEngine : ITtsEngine
{
    private const string ApiUrl = "https://tiktok-tts.weilnet.workers.dev/api/generation";
    private const int MaxChunkLength = 200;

    private readonly HttpClient _http = HttpClientFactory.GetClient("tiktok");

    public string EngineId => EngineIds.TikTok;
    public bool IsConfigured => true;

    public IReadOnlyList<EngineParameterDef> GetParameters() => Array.Empty<EngineParameterDef>();

    public void Configure(string configJson) { }

    public async Task<AudioData> SynthesizeAsync(string text, string voiceId, SynthParams parameters)
    {
        if (string.IsNullOrWhiteSpace(text)) return AudioData.Empty;

        var chunks = SplitIntoChunks(text, MaxChunkLength);
        var pcmChunks = new List<byte[]>();
        WaveFormat? format = null;

        foreach (var chunk in chunks)
        {
            var body = JsonConvert.SerializeObject(new { text = chunk, voice = voiceId });
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            try
            {
                var response = await _http.PostAsync(ApiUrl, content);
                if (!response.IsSuccessStatusCode) return AudioData.Empty;

                var json = await response.Content.ReadAsStringAsync();
                var obj = JObject.Parse(json);
                var b64 = obj["data"]?.Value<string>() ?? string.Empty;
                if (string.IsNullOrEmpty(b64)) return AudioData.Empty;

                var mp3Bytes = Convert.FromBase64String(b64);
                using var ms = new MemoryStream(mp3Bytes);
                using var reader = new Mp3FileReader(ms);
                using var pcmStream = WaveFormatConversionStream.CreatePcmStream(reader);
                using var pcmMs = new MemoryStream();
                await pcmStream.CopyToAsync(pcmMs);
                format ??= pcmStream.WaveFormat;
                pcmChunks.Add(pcmMs.ToArray());
            }
            catch
            {
                return AudioData.Empty;
            }
        }

        if (pcmChunks.Count == 0 || format == null) return AudioData.Empty;

        var combined = new byte[pcmChunks.Sum(c => c.Length)];
        var offset = 0;
        foreach (var chunk in pcmChunks)
        {
            Buffer.BlockCopy(chunk, 0, combined, offset, chunk.Length);
            offset += chunk.Length;
        }

        return new AudioData { Samples = combined, Format = format };
    }

    public Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync() =>
        Task.FromResult<IReadOnlyList<VoiceInfo>>(Voices);

    public void Dispose() { }

    private static List<string> SplitIntoChunks(string text, int maxLength)
    {
        var chunks = new List<string>();
        var words = text.Split(' ');
        var sb = new StringBuilder();

        foreach (var word in words)
        {
            if (sb.Length > 0 && sb.Length + 1 + word.Length > maxLength)
            {
                chunks.Add(sb.ToString());
                sb.Clear();
            }
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(word);
        }

        if (sb.Length > 0)
            chunks.Add(sb.ToString());

        return chunks;
    }

    private static readonly IReadOnlyList<VoiceInfo> Voices = new[]
    {
        new VoiceInfo { Id = "en_us_001",                      Name = "US Female 1",          Locale = "en-US", Gender = "Female" },
        new VoiceInfo { Id = "en_us_002",                      Name = "US Female 2",          Locale = "en-US", Gender = "Female" },
        new VoiceInfo { Id = "en_us_006",                      Name = "US Male 1",            Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_us_007",                      Name = "US Male 2",            Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_us_009",                      Name = "US Male 3",            Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_us_010",                      Name = "US Male 4",            Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_au_001",                      Name = "AU Female",            Locale = "en-AU", Gender = "Female" },
        new VoiceInfo { Id = "en_au_002",                      Name = "AU Male",              Locale = "en-AU", Gender = "Male" },
        new VoiceInfo { Id = "en_uk_001",                      Name = "UK Male 1",            Locale = "en-GB", Gender = "Male" },
        new VoiceInfo { Id = "en_uk_003",                      Name = "UK Male 2",            Locale = "en-GB", Gender = "Male" },
        new VoiceInfo { Id = "en_male_narration",              Name = "Narrator",             Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_male_funny",                  Name = "Wacky",                Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_female_emotional",            Name = "Peaceful",             Locale = "en-US", Gender = "Female" },
        new VoiceInfo { Id = "en_male_cody",                   Name = "Serious",              Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_us_ghostface",                Name = "Ghost Face",           Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_us_chewbacca",                Name = "Chewbacca",            Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_us_c3po",                     Name = "C3PO",                 Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_us_stitch",                   Name = "Stitch",               Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_us_stormtrooper",             Name = "Stormtrooper",         Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_us_rocket",                   Name = "Rocket",               Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_female_madam_leota",          Name = "Madame Leota",         Locale = "en-US", Gender = "Female" },
        new VoiceInfo { Id = "en_male_ghosthost",              Name = "Ghost Host",           Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_male_pirate",                 Name = "Pirate",               Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_female_f08_salut_damour",     Name = "Alto",                 Locale = "en-US", Gender = "Female" },
        new VoiceInfo { Id = "en_male_m03_lobby",              Name = "Tenor",                Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_male_m03_sunshine_soon",      Name = "Sunshine Soon",        Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_female_f08_warmy_breeze",     Name = "Warmy Breeze",         Locale = "en-US", Gender = "Female" },
        new VoiceInfo { Id = "en_female_ht_f08_glorious",      Name = "Glorious",             Locale = "en-US", Gender = "Female" },
        new VoiceInfo { Id = "en_male_sing_funny_it_goes_up",  Name = "It Goes Up",           Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_male_m2_xhxs_m03_silly",     Name = "Chipmunk",             Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_female_ht_f08_wonderful_world", Name = "Dramatic",           Locale = "en-US", Gender = "Female" },
        new VoiceInfo { Id = "fr_001",                         Name = "French Male 1",        Locale = "fr-FR", Gender = "Male" },
        new VoiceInfo { Id = "fr_002",                         Name = "French Male 2",        Locale = "fr-FR", Gender = "Male" },
        new VoiceInfo { Id = "de_001",                         Name = "German Female",        Locale = "de-DE", Gender = "Female" },
        new VoiceInfo { Id = "de_002",                         Name = "German Male",          Locale = "de-DE", Gender = "Male" },
        new VoiceInfo { Id = "es_002",                         Name = "Spanish Male",         Locale = "es-ES", Gender = "Male" },
        new VoiceInfo { Id = "es_mx_002",                      Name = "Spanish MX Male",      Locale = "es-MX", Gender = "Male" },
        new VoiceInfo { Id = "br_001",                         Name = "Portuguese BR Female 1", Locale = "pt-BR", Gender = "Female" },
        new VoiceInfo { Id = "br_003",                         Name = "Portuguese BR Female 2", Locale = "pt-BR", Gender = "Female" },
        new VoiceInfo { Id = "br_004",                         Name = "Portuguese BR Female 3", Locale = "pt-BR", Gender = "Female" },
        new VoiceInfo { Id = "br_005",                         Name = "Portuguese BR Male",   Locale = "pt-BR", Gender = "Male" },
        new VoiceInfo { Id = "id_001",                         Name = "Indonesian Female",    Locale = "id-ID", Gender = "Female" },
        new VoiceInfo { Id = "jp_001",                         Name = "Japanese Female 1",    Locale = "ja-JP", Gender = "Female" },
        new VoiceInfo { Id = "jp_003",                         Name = "Japanese Female 2",    Locale = "ja-JP", Gender = "Female" },
        new VoiceInfo { Id = "jp_005",                         Name = "Japanese Female 3",    Locale = "ja-JP", Gender = "Female" },
        new VoiceInfo { Id = "jp_006",                         Name = "Japanese Male",        Locale = "ja-JP", Gender = "Male" },
        new VoiceInfo { Id = "kr_002",                         Name = "Korean Male 1",        Locale = "ko-KR", Gender = "Male" },
        new VoiceInfo { Id = "kr_003",                         Name = "Korean Female",        Locale = "ko-KR", Gender = "Female" },
        new VoiceInfo { Id = "kr_004",                         Name = "Korean Male 2",        Locale = "ko-KR", Gender = "Male" },
    };
}
