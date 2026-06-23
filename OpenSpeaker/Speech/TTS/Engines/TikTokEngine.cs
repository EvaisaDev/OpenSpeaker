using System.IO;
using System.Net.Http;
using System.Text;
using NAudio.Wave;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Models;
namespace OpenSpeaker.TTS.Engines;

public class TikTokEngine : ITtsEngine
{
    private const string ApiUrl = "https://tiktok-tts.weilnet.workers.dev/api/generation";
    private const int MaxChunkLength = 200;

    private static readonly HttpClient _http = CreateClient();

    private static HttpClient CreateClient()
    {
        var handler = new WinHttpHandler
        {
            WindowsProxyUsePolicy = WindowsProxyUsePolicy.DoNotUseProxy,
            CheckCertificateRevocationList = false,
            ServerCertificateValidationCallback = (_, _, _, _) => true,
        };
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "com.zhiliaoapp.musically/2022600030 (Linux; U; Android 7.1.2; es_ES; SM-G988N; Build/NRD90M;tt-ok/3.12.13.1)");
        return client;
    }

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
            var body = System.Text.Json.JsonSerializer.Serialize(new { text = chunk, voice = voiceId });
            var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            HttpResponseMessage response;
            try { response = await _http.SendAsync(request); }
            catch (HttpRequestException ex)
            {
                var inner = ex.InnerException?.Message ?? "none";
                throw new HttpRequestException($"TikTok TTS request failed: {ex.Message} | Inner: {inner}", ex);
            }
            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"TikTok TTS API returned {(int)response.StatusCode}: {responseText}");

            var obj = JObject.Parse(responseText);
            var b64 = obj["data"]?.Value<string>() ?? string.Empty;
            if (string.IsNullOrEmpty(b64))
                throw new InvalidOperationException($"TikTok TTS API returned no audio. Response: {responseText}");

            var mp3Bytes = Convert.FromBase64String(b64);
            var decoded = await AudioDecoder.DecodeAsync(mp3Bytes);
            format ??= decoded.Format;
            pcmChunks.Add(decoded.Samples);
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
        new VoiceInfo { Id = "en_us_001",                        Name = "US Female 1",             Locale = "en-US", Gender = "Female" },
        new VoiceInfo { Id = "en_us_002",                        Name = "Jessie",                  Locale = "en-US", Gender = "Female" },
        new VoiceInfo { Id = "en_us_006",                        Name = "Joey",                    Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_us_007",                        Name = "Professor",               Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_us_009",                        Name = "Scientist",               Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_us_010",                        Name = "Confidence",              Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_au_001",                        Name = "Metro (Eddie)",           Locale = "en-AU", Gender = "Female" },
        new VoiceInfo { Id = "en_au_002",                        Name = "Smooth (Alex)",           Locale = "en-AU", Gender = "Male" },
        new VoiceInfo { Id = "en_uk_001",                        Name = "Narrator (Chris)",        Locale = "en-GB", Gender = "Male" },
        new VoiceInfo { Id = "en_uk_003",                        Name = "UK Male 2",               Locale = "en-GB", Gender = "Male" },
        new VoiceInfo { Id = "en_male_narration",                Name = "Story Teller",            Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_male_funny",                    Name = "Wacky",                   Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_female_emotional",              Name = "Peaceful",                Locale = "en-US", Gender = "Female" },
        new VoiceInfo { Id = "en_female_samc",                   Name = "Empathetic",              Locale = "en-US", Gender = "Female" },
        new VoiceInfo { Id = "en_male_cody",                     Name = "Serious",                 Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_male_jarvis",                   Name = "Alfred",                  Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_male_santa_narration",          Name = "Author",                  Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_female_betty",                  Name = "Bae",                     Locale = "en-US", Gender = "Female" },
        new VoiceInfo { Id = "en_female_makeup",                 Name = "Beauty Guru",             Locale = "en-US", Gender = "Female" },
        new VoiceInfo { Id = "en_female_richgirl",               Name = "Bestie",                  Locale = "en-US", Gender = "Female" },
        new VoiceInfo { Id = "en_male_cupid",                    Name = "Cupid",                   Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_female_shenna",                 Name = "Debutante",               Locale = "en-US", Gender = "Female" },
        new VoiceInfo { Id = "en_male_ghosthost",                Name = "Ghost Host",              Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_female_grandma",                Name = "Grandma",                 Locale = "en-US", Gender = "Female" },
        new VoiceInfo { Id = "en_male_ukneighbor",               Name = "Lord Cringe",             Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_male_wizard",                   Name = "Magician",                Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_male_trevor",                   Name = "Marty",                   Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_male_deadpool",                 Name = "Mr. GoodGuy",             Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_male_ukbutler",                 Name = "Mr. Meticulous",          Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_male_pirate",                   Name = "Pirate",                  Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_male_santa",                    Name = "Santa",                   Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_male_santa_effect",             Name = "Santa (w/ effect)",       Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_female_pansino",                Name = "Varsity",                 Locale = "en-US", Gender = "Female" },
        new VoiceInfo { Id = "en_male_grinch",                   Name = "Trickster (Grinch)",      Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_us_ghostface",                  Name = "Ghostface (Scream)",      Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_us_chewbacca",                  Name = "Chewbacca",               Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_us_c3po",                       Name = "C-3PO",                   Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_us_stitch",                     Name = "Stitch",                  Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_us_stormtrooper",               Name = "Stormtrooper",            Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_us_rocket",                     Name = "Rocket",                  Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_female_madam_leota",            Name = "Madame Leota",            Locale = "en-US", Gender = "Female" },
        new VoiceInfo { Id = "en_male_sing_deep_jingle",         Name = "Song: Caroler",           Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_male_m03_classical",            Name = "Song: Classic Electric",  Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_female_f08_salut_damour",       Name = "Song: Cottagecore",       Locale = "en-US", Gender = "Female" },
        new VoiceInfo { Id = "en_male_m2_xhxs_m03_christmas",   Name = "Song: Cozy",              Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_female_f08_warmy_breeze",       Name = "Song: Open Mic",          Locale = "en-US", Gender = "Female" },
        new VoiceInfo { Id = "en_female_ht_f08_halloween",       Name = "Song: Opera",             Locale = "en-US", Gender = "Female" },
        new VoiceInfo { Id = "en_female_ht_f08_glorious",        Name = "Song: Euphoric",          Locale = "en-US", Gender = "Female" },
        new VoiceInfo { Id = "en_male_sing_funny_it_goes_up",    Name = "Song: Hypetrain",         Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_male_m03_lobby",                Name = "Song: Jingle",            Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_female_ht_f08_wonderful_world", Name = "Song: Melodrama",         Locale = "en-US", Gender = "Female" },
        new VoiceInfo { Id = "en_female_ht_f08_newyear",         Name = "Song: NYE 2023",          Locale = "en-US", Gender = "Female" },
        new VoiceInfo { Id = "en_male_sing_funny_thanksgiving",  Name = "Song: Thanksgiving",      Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_male_m03_sunshine_soon",        Name = "Song: Toon Beat",         Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "en_female_f08_twinkle",            Name = "Song: Pop Lullaby",       Locale = "en-US", Gender = "Female" },
        new VoiceInfo { Id = "en_male_m2_xhxs_m03_silly",       Name = "Song: Quirky Time",       Locale = "en-US", Gender = "Male" },
        new VoiceInfo { Id = "fr_001",                           Name = "French Male 1",           Locale = "fr-FR", Gender = "Male" },
        new VoiceInfo { Id = "fr_002",                           Name = "French Male 2",           Locale = "fr-FR", Gender = "Male" },
        new VoiceInfo { Id = "de_001",                           Name = "German Female",           Locale = "de-DE", Gender = "Female" },
        new VoiceInfo { Id = "de_002",                           Name = "German Male",             Locale = "de-DE", Gender = "Male" },
        new VoiceInfo { Id = "id_male_darma",                    Name = "Darma",                   Locale = "id-ID", Gender = "Male" },
        new VoiceInfo { Id = "id_female_icha",                   Name = "Icha",                    Locale = "id-ID", Gender = "Female" },
        new VoiceInfo { Id = "id_female_noor",                   Name = "Noor",                    Locale = "id-ID", Gender = "Female" },
        new VoiceInfo { Id = "id_male_putra",                    Name = "Putra",                   Locale = "id-ID", Gender = "Male" },
        new VoiceInfo { Id = "it_male_m18",                      Name = "Italian Male",            Locale = "it-IT", Gender = "Male" },
        new VoiceInfo { Id = "jp_001",                           Name = "Miho",                    Locale = "ja-JP", Gender = "Female" },
        new VoiceInfo { Id = "jp_003",                           Name = "Keiko",                   Locale = "ja-JP", Gender = "Female" },
        new VoiceInfo { Id = "jp_005",                           Name = "Sakura",                  Locale = "ja-JP", Gender = "Female" },
        new VoiceInfo { Id = "jp_006",                           Name = "Naoki",                   Locale = "ja-JP", Gender = "Male" },
        new VoiceInfo { Id = "jp_male_osada",                    Name = "Morisuke",                Locale = "ja-JP", Gender = "Male" },
        new VoiceInfo { Id = "jp_male_matsuo",                   Name = "Matsuo",                  Locale = "ja-JP", Gender = "Male" },
        new VoiceInfo { Id = "jp_female_machikoriiita",          Name = "Machikoriiita",           Locale = "ja-JP", Gender = "Female" },
        new VoiceInfo { Id = "jp_male_matsudake",                Name = "Matsudake",               Locale = "ja-JP", Gender = "Male" },
        new VoiceInfo { Id = "jp_male_shuichiro",                Name = "Shuichiro",               Locale = "ja-JP", Gender = "Male" },
        new VoiceInfo { Id = "jp_female_rei",                    Name = "Maruyama Rei",            Locale = "ja-JP", Gender = "Female" },
        new VoiceInfo { Id = "jp_male_hikakin",                  Name = "Hikakin",                 Locale = "ja-JP", Gender = "Male" },
        new VoiceInfo { Id = "jp_female_yagishaki",              Name = "Yagi Saki",               Locale = "ja-JP", Gender = "Female" },
        new VoiceInfo { Id = "kr_002",                           Name = "Korean Male 1",           Locale = "ko-KR", Gender = "Male" },
        new VoiceInfo { Id = "kr_003",                           Name = "Korean Female",           Locale = "ko-KR", Gender = "Female" },
        new VoiceInfo { Id = "kr_004",                           Name = "Korean Male 2",           Locale = "ko-KR", Gender = "Male" },
        new VoiceInfo { Id = "br_003",                           Name = "Júlia",                   Locale = "pt-BR", Gender = "Female" },
        new VoiceInfo { Id = "br_004",                           Name = "Ana",                     Locale = "pt-BR", Gender = "Female" },
        new VoiceInfo { Id = "br_005",                           Name = "Lucas",                   Locale = "pt-BR", Gender = "Male" },
        new VoiceInfo { Id = "pt_female_lhays",                  Name = "Lhays Macedo",            Locale = "pt-PT", Gender = "Female" },
        new VoiceInfo { Id = "pt_female_laizza",                 Name = "Laizza",                  Locale = "pt-PT", Gender = "Female" },
        new VoiceInfo { Id = "es_002",                           Name = "Spanish Male",            Locale = "es-ES", Gender = "Male" },
        new VoiceInfo { Id = "es_male_m3",                       Name = "Julio",                   Locale = "es-ES", Gender = "Male" },
        new VoiceInfo { Id = "es_female_f6",                     Name = "Alejandra",               Locale = "es-ES", Gender = "Female" },
        new VoiceInfo { Id = "es_female_fp1",                    Name = "Mariana",                 Locale = "es-ES", Gender = "Female" },
        new VoiceInfo { Id = "es_mx_002",                        Name = "Álex (Warm)",             Locale = "es-MX", Gender = "Male" },
        new VoiceInfo { Id = "es_mx_female_supermom",            Name = "Super Mamá",              Locale = "es-MX", Gender = "Female" },
    };
}
