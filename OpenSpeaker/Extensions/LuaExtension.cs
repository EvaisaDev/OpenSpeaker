using System.IO;
using System.Net.Http;
using Lua;
using Lua.Standard;
using NAudio.Wave;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Import;
using OpenSpeaker.ThingsIDKWhereToPut.Logging;
using OpenSpeaker.TTS;
namespace OpenSpeaker.Extensions;

public record ExtAuthField(string Key, string Label, string Type);
public record ExtSettingField(string Key, string Label, string Type, string Default, string[] Options);
public record MessageFilterContext(
    string Id,
    string Username,
    string DisplayName,
    string Nickname,
    bool IsSubscriber,
    bool IsMod,
    bool IsVip,
    bool IsBroadcaster,
    bool IsRegular,
    bool IsIgnored,
    bool IsForced
);

public class LuaExtension : IDisposable
{
    private readonly LuaState _state;
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly HttpClient _http = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task<byte[]>> _asyncJobs = new();
    private readonly Dictionary<string, Dictionary<string, string>> _authByEngine = new();
    private readonly List<LuaTtsEngine> _speechEngines = new();
    private List<ExtSettingField> _settingFields = new();
    private Dictionary<string, string> _settingValues = new();
    private IAppLogger? _logger;

    public string ExtensionId { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool HasMessageFilter { get; private set; }
    public IReadOnlyList<LuaTtsEngine> SpeechEngines => _speechEngines;
    public IReadOnlyList<ExtSettingField> SettingFields => _settingFields;
    public IReadOnlyDictionary<string, string> MigrationVoiceRemap { get; private set; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, MigrationEngineConfig> MigrationEngineConfigs { get; private set; } = new Dictionary<string, MigrationEngineConfig>();

    private LuaExtension()
    {
        _state = LuaState.Create();
        _state.OpenStandardLibraries();
    }

    public static async Task<LuaExtension> CreateAsync(string luaFilePath, IAppLogger? logger = null)
    {
        var ext = new LuaExtension();
        ext._logger = logger;
        ext.RegisterApisOn(ext._state);
        await ext._state.DoFileAsync(luaFilePath);

        var extFnVal = ext._state.Environment["Extension"];
        if (!extFnVal.TryRead<LuaFunction>(out var extFn))
            throw new InvalidOperationException("Extension missing Extension() function.");

        var results = await ext._state.CallAsync(extFn, Array.Empty<LuaValue>());
        if (results.Length == 0 || !results[0].TryRead<LuaTable>(out var meta))
            throw new InvalidOperationException("Extension() must return a table.");

        ext.ExtensionId = "ext:" + meta["id"].Read<string>();
        ext.DisplayName = meta["name"].Read<string>();
        meta["description"].TryRead<string>(out var desc);
        ext.Description = desc ?? string.Empty;
        ext.HasMessageFilter = ext._state.Environment["OnMessage"].TryRead<LuaFunction>(out _);
        return ext;
    }

    internal void SetSettings(Dictionary<string, string> values) => _settingValues = new Dictionary<string, string>(values);

    private LuaValue ResolveSetting(ExtSettingField field)
    {
        var raw = _settingValues.TryGetValue(field.Key, out var v) ? v : field.Default;
        if (field.Type == "checkbox")
            return raw == "true";
        if (field.Type == "number" && double.TryParse(raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d))
            return d;
        return raw;
    }

    internal string GetSettingValue(string key) =>
        _settingValues.TryGetValue(key, out var v) ? v
        : _settingFields.FirstOrDefault(f => f.Key == key)?.Default ?? string.Empty;

    internal void SetAuth(string engineId, string configJson)
    {
        _logger?.Info($"[{ExtensionId}] SetAuth called: engineId={engineId} configJson={configJson ?? "(null)"}");
        if (string.IsNullOrEmpty(configJson) || configJson == "{}")
        {
            _logger?.Warn($"[{ExtensionId}] SetAuth: config is empty or default '{{}}', skipping — auth will NOT be set for {engineId}");
            return;
        }
        try
        {
            var obj = JObject.Parse(configJson);
            _authByEngine[engineId] = obj.Properties()
                .ToDictionary(p => p.Name, p => p.Value.Value<string>() ?? string.Empty);
            _logger?.Info($"[{ExtensionId}] SetAuth: stored {_authByEngine[engineId].Count} keys for {engineId}: [{string.Join(", ", _authByEngine[engineId].Select(kv => $"{kv.Key}={(!string.IsNullOrEmpty(kv.Value) ? "<set>" : "<empty>")}"))}]");
        }
        catch (Exception ex) { _logger?.Error($"[{ExtensionId}] SetAuth parse error: {ex.Message}"); }
    }

    internal async Task<AudioData> SynthesizeAsync(string engineId, string text, string voiceId, SynthParams parameters)
    {
        if (string.IsNullOrWhiteSpace(text)) return AudioData.Empty;

        LuaValue result = LuaValue.Nil;
        await _stateLock.WaitAsync();
        try
        {
            if (!_state.Environment["GenerateSpeech"].TryRead<LuaFunction>(out var fn)) return AudioData.Empty;
            var luaId = StripPrefix(engineId);
            var results = await _state.CallAsync(fn, new LuaValue[] { luaId, voiceId, text, LuaValue.Nil });
            result = results.Length > 0 ? results[0] : LuaValue.Nil;
        }
        catch (Exception ex)
        {
            _logger?.Error($"[{ExtensionId}] GenerateSpeech error: {ex.Message}");
            return AudioData.Empty;
        }
        finally
        {
            _stateLock.Release();
        }

        if (result.TryRead<LuaTable>(out var table) &&
            table["async_job"].TryRead<string>(out var jobId) &&
            _asyncJobs.TryRemove(jobId, out var jobTask))
        {
            table["format"].TryRead<string>(out var fmt);
            try
            {
                var bytes = await jobTask;
                return await ToAudioDataAsync(bytes, fmt ?? "mp3");
            }
            catch (Exception ex)
            {
                _logger?.Error($"[{ExtensionId}] Async job error: {ex.Message}");
                return AudioData.Empty;
            }
        }

        return await ProcessReturnAsync(result);
    }

    internal async Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync(string engineId)
    {
        await _stateLock.WaitAsync();
        try { return await GetVoicesInternalAsync(engineId); }
        finally { _stateLock.Release(); }
    }

    private async Task<IReadOnlyList<VoiceInfo>> GetVoicesInternalAsync(string engineId)
    {
        if (!_state.Environment["GetVoices"].TryRead<LuaFunction>(out var fn))
        {
            _logger?.Warn($"[{ExtensionId}] GetVoices function not found in script");
            return Array.Empty<VoiceInfo>();
        }
        var luaId = StripPrefix(engineId);
        try
        {
            var results = await _state.CallAsync(fn, new LuaValue[] { luaId });
            if (results.Length == 0)
            {
                _logger?.Warn($"[{ExtensionId}] GetVoices returned no values");
                return Array.Empty<VoiceInfo>();
            }
            if (!results[0].TryRead<LuaTable>(out var table))
            {
                _logger?.Warn($"[{ExtensionId}] GetVoices returned {results[0]} instead of a table");
                return Array.Empty<VoiceInfo>();
            }
            if (table.ArrayLength == 0)
            {
                _logger?.Warn($"[{ExtensionId}] GetVoices returned an empty table");
                return Array.Empty<VoiceInfo>();
            }

            var list = new List<VoiceInfo>();
            for (var i = 1; i <= table.ArrayLength; i++)
            {
                if (!table[i].TryRead<LuaTable>(out var entry)) continue;
                if (!entry["id"].TryRead<string>(out var id) || !entry["name"].TryRead<string>(out var name)) continue;
                entry["locale"].TryRead<string>(out var locale);
                entry["gender"].TryRead<string>(out var gender);
                list.Add(new VoiceInfo
                {
                    Id = id,
                    Name = name,
                    Locale = locale ?? string.Empty,
                    Gender = gender ?? string.Empty,
                    EngineId = engineId
                });
            }
            return list;
        }
        catch (Exception ex)
        {
            _logger?.Error($"[{ExtensionId}] GetVoices error: {ex.Message}");
            return Array.Empty<VoiceInfo>();
        }
    }

    internal async Task<string> ProcessMessageAsync(MessageFilterContext ctx, string message)
    {
        await _stateLock.WaitAsync();
        try
        {
            if (!_state.Environment["OnMessage"].TryRead<LuaFunction>(out var fn))
                return message;
            var userTable = BuildUserTable(ctx);
            var results = await _state.CallAsync(fn, new LuaValue[] { userTable, message });
            if (results.Length > 0 && results[0].TryRead<string>(out var modified))
                return modified;
            return message;
        }
        catch { return message; }
        finally { _stateLock.Release(); }
    }

    private static LuaTable BuildUserTable(MessageFilterContext ctx)
    {
        var t = new LuaTable();
        t["id"] = ctx.Id;
        t["username"] = ctx.Username;
        t["display_name"] = ctx.DisplayName;
        t["nickname"] = ctx.Nickname;
        t["is_subscriber"] = ctx.IsSubscriber;
        t["is_mod"] = ctx.IsMod;
        t["is_vip"] = ctx.IsVip;
        t["is_broadcaster"] = ctx.IsBroadcaster;
        t["is_regular"] = ctx.IsRegular;
        t["is_ignored"] = ctx.IsIgnored;
        t["is_forced"] = ctx.IsForced;
        return t;
    }

    private void RegisterApisOn(LuaState state)
    {
        var httpTable = new LuaTable();
        httpTable["get"] = new LuaFunction(async (ctx, ct) =>
        {
            var url = ctx.GetArgument<string>(0);
            var headers = GetOptionalTable(ctx, 1);
            var result = await HttpGetAsync(url, headers, ct);
            return ctx.Return(result);
        });
        httpTable["post"] = new LuaFunction(async (ctx, ct) =>
        {
            var url = ctx.GetArgument<string>(0);
            var body = ctx.GetArgument<string>(1);
            var contentType = ctx.HasArgument(2) ? ctx.GetArgument<string>(2) : "application/json";
            var headers = GetOptionalTable(ctx, 3);
            var result = await HttpPostAsync(url, body, contentType, headers, ct);
            return ctx.Return(result);
        });
        httpTable["get_bytes"] = new LuaFunction(async (ctx, ct) =>
        {
            var url = ctx.GetArgument<string>(0);
            var headers = GetOptionalTable(ctx, 1);
            var result = await HttpGetBytesAsync(url, headers, ct);
            return ctx.Return(result);
        });
        httpTable["post_bytes"] = new LuaFunction(async (ctx, ct) =>
        {
            var url = ctx.GetArgument<string>(0);
            var body = ctx.GetArgument<string>(1);
            var contentType = ctx.HasArgument(2) ? ctx.GetArgument<string>(2) : "application/json";
            var headers = GetOptionalTable(ctx, 3);
            var result = await HttpPostBytesAsync(url, body, contentType, headers, ct);
            return ctx.Return(result);
        });
        httpTable["get_bytes_async"] = new LuaFunction((ctx, ct) =>
        {
            var url = ctx.GetArgument<string>(0);
            var headers = GetOptionalTable(ctx, 1);
            var jobId = Guid.NewGuid().ToString("N");
            _asyncJobs[jobId] = HttpGetBytesRawAsync(url, headers, CancellationToken.None);
            return new(ctx.Return(jobId));
        });
        httpTable["post_bytes_async"] = new LuaFunction((ctx, ct) =>
        {
            var url = ctx.GetArgument<string>(0);
            var body = ctx.GetArgument<string>(1);
            var contentType = ctx.HasArgument(2) ? ctx.GetArgument<string>(2) : "application/json";
            var headers = GetOptionalTable(ctx, 3);
            var jobId = Guid.NewGuid().ToString("N");
            _asyncJobs[jobId] = HttpPostBytesRawAsync(url, body, contentType, headers, CancellationToken.None);
            return new(ctx.Return(jobId));
        });
        state.Environment["http"] = httpTable;

        var jsonTable = new LuaTable();
        jsonTable["parse"] = new LuaFunction((ctx, ct) =>
        {
            var str = ctx.GetArgument<string>(0);
            try { return new(ctx.Return(JsonToLua(JToken.Parse(str)))); }
            catch { return new(ctx.Return(LuaValue.Nil)); }
        });
        jsonTable["stringify"] = new LuaFunction((ctx, ct) =>
        {
            var val = ctx.GetArgument<LuaValue>(0);
            try { return new(ctx.Return(LuaToJson(val).ToString(Newtonsoft.Json.Formatting.None))); }
            catch { return new(ctx.Return("null")); }
        });
        state.Environment["json"] = jsonTable;

        var b64Table = new LuaTable();
        b64Table["encode"] = new LuaFunction((ctx, ct) =>
        {
            var s = ctx.GetArgument<string>(0);
            return new(ctx.Return(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(s))));
        });
        b64Table["decode"] = new LuaFunction((ctx, ct) =>
        {
            var s = ctx.GetArgument<string>(0);
            try { return new(ctx.Return(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(s)))); }
            catch { return new(ctx.Return(string.Empty)); }
        });
        state.Environment["base64"] = b64Table;

        state.Environment["RegisterSpeechEngine"] = new LuaFunction((ctx, ct) =>
        {
            if (!ctx.HasArgument(0)) return new(0);
            var val = ctx.GetArgument<LuaValue>(0);
            if (!val.TryRead<LuaTable>(out var def)) return new(0);
            if (!def["id"].TryRead<string>(out var id)) return new(0);
            def["name"].TryRead<string>(out var name);
            var authFields = ParseAuthFields(def["auth"]);
            _speechEngines.Add(new LuaTtsEngine(this, "ext:" + id, name ?? id, authFields));
            return new(0);
        });

        state.Environment["RegisterSettings"] = new LuaFunction((ctx, ct) =>
        {
            if (!ctx.HasArgument(0)) return new(0);
            var val = ctx.GetArgument<LuaValue>(0);
            if (!val.TryRead<LuaTable>(out var table)) return new(0);
            _settingFields = ParseSettingFields(table);
            return new(0);
        });

        state.Environment["GetSettings"] = new LuaFunction((ctx, ct) =>
        {
            var t = new LuaTable();
            foreach (var field in _settingFields)
                t[field.Key] = ResolveSetting(field);
            return new(ctx.Return(t));
        });

        state.Environment["GetSetting"] = new LuaFunction((ctx, ct) =>
        {
            if (!ctx.HasArgument(0)) return new(ctx.Return(LuaValue.Nil));
            var key = ctx.GetArgument<string>(0);
            var field = _settingFields.FirstOrDefault(f => f.Key == key);
            return field is null ? new(ctx.Return(LuaValue.Nil)) : new(ctx.Return(ResolveSetting(field)));
        });

        state.Environment["GetAuth"] = new LuaFunction((ctx, ct) =>
        {
            var t = new LuaTable();
            if (ctx.HasArgument(0) && ctx.GetArgument<string>(0) is { } rawId)
            {
                var fullId = "ext:" + rawId;
                var knownKeys = _authByEngine.Count == 0 ? "(empty)" : string.Join(", ", _authByEngine.Keys);
                _logger?.Info($"[{ExtensionId}] GetAuth: looking up '{fullId}', stored keys: {knownKeys}");
                if (_authByEngine.TryGetValue(fullId, out var vals))
                    foreach (var kv in vals) t[kv.Key] = kv.Value;
                else
                    _logger?.Warn($"[{ExtensionId}] GetAuth: no auth found for '{fullId}'");
            }
            return new(ctx.Return(t));
        });

        state.Environment["GetVoiceSettings"] = new LuaFunction((ctx, ct) =>
            new ValueTask<int>(ctx.Return(new LuaTable())));

        state.Environment["RegisterMigrationHook"] = new LuaFunction((ctx, ct) =>
        {
            if (!ctx.HasArgument(0)) return new(0);
            if (!ctx.GetArgument<LuaValue>(0).TryRead<LuaTable>(out var table)) return new(0);

            var voiceRemap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (table["remap_voices"].TryRead<LuaTable>(out var voiceTable))
                foreach (var kvp in voiceTable)
                    if (kvp.Key.TryRead<string>(out var from) && kvp.Value.TryRead<string>(out var to))
                        voiceRemap[from] = to;

            var engineConfigs = new Dictionary<string, MigrationEngineConfig>(StringComparer.OrdinalIgnoreCase);
            if (table["remap_config"].TryRead<LuaTable>(out var configTable))
                foreach (var kvp in configTable)
                    if (kvp.Key.TryRead<string>(out var sbEngine) && kvp.Value.TryRead<LuaTable>(out var entry))
                    {
                        entry["engine_id"].TryRead<string>(out var targetEngineId);
                        var fieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        if (entry["fields"].TryRead<LuaTable>(out var fieldsTable))
                            foreach (var f in fieldsTable)
                                if (f.Key.TryRead<string>(out var targetField) && f.Value.TryRead<string>(out var sourceField))
                                    fieldMap[targetField] = sourceField;
                        if (!string.IsNullOrEmpty(targetEngineId))
                            engineConfigs[sbEngine] = new MigrationEngineConfig(targetEngineId!, fieldMap);
                    }

            MigrationVoiceRemap = voiceRemap;
            MigrationEngineConfigs = engineConfigs;
            return new(0);
        });

        state.Environment["log"] = new LuaFunction((ctx, ct) =>
        {
            if (ctx.HasArgument(0))
            {
                var msg = $"[{ExtensionId}] {ctx.GetArgument<string>(0)}";
                _logger?.Info(msg);
                System.Diagnostics.Debug.WriteLine(msg);
            }
            return new(0);
        });
    }

    private static LuaTable? GetOptionalTable(LuaFunctionExecutionContext ctx, int index)
    {
        if (!ctx.HasArgument(index)) return null;
        var val = ctx.GetArgument<LuaValue>(index);
        return val.TryRead<LuaTable>(out var t) ? t : null;
    }

    private async Task<LuaTable> HttpGetAsync(string url, LuaTable? headers, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            ApplyHeaders(req, headers);
            using var resp = await _http.SendAsync(req, ct);
            return MakeBody(resp.IsSuccessStatusCode, (int)resp.StatusCode, await resp.Content.ReadAsStringAsync(ct));
        }
        catch (Exception ex) { return MakeBody(false, 0, ex.Message); }
    }

    private async Task<LuaTable> HttpPostAsync(string url, string body, string contentType, LuaTable? headers, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, contentType)
            };
            ApplyHeaders(req, headers);
            using var resp = await _http.SendAsync(req, ct);
            return MakeBody(resp.IsSuccessStatusCode, (int)resp.StatusCode, await resp.Content.ReadAsStringAsync(ct));
        }
        catch (Exception ex) { return MakeBody(false, 0, ex.Message); }
    }

    private async Task<LuaTable> HttpGetBytesAsync(string url, LuaTable? headers, CancellationToken ct)
    {
        try
        {
            var bytes = await HttpGetBytesRawAsync(url, headers, ct);
            return MakeBytes(true, 200, Convert.ToBase64String(bytes));
        }
        catch { return MakeBytes(false, 0, string.Empty); }
    }

    private async Task<LuaTable> HttpPostBytesAsync(string url, string body, string contentType, LuaTable? headers, CancellationToken ct)
    {
        try
        {
            var bytes = await HttpPostBytesRawAsync(url, body, contentType, headers, ct);
            return MakeBytes(true, 200, Convert.ToBase64String(bytes));
        }
        catch { return MakeBytes(false, 0, string.Empty); }
    }

    private async Task<byte[]> HttpGetBytesRawAsync(string url, LuaTable? headers, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyHeaders(req, headers);
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsByteArrayAsync(ct);
    }

    private async Task<byte[]> HttpPostBytesRawAsync(string url, string body, string contentType, LuaTable? headers, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, contentType)
        };
        ApplyHeaders(req, headers);
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsByteArrayAsync(ct);
    }

    private static LuaTable MakeBody(bool ok, int status, string body)
    {
        var t = new LuaTable();
        t["ok"] = ok;
        t["status"] = (double)status;
        t["body"] = body;
        return t;
    }

    private static LuaTable MakeBytes(bool ok, int status, string data)
    {
        var t = new LuaTable();
        t["ok"] = ok;
        t["status"] = (double)status;
        t["data"] = data;
        return t;
    }

    private static void ApplyHeaders(HttpRequestMessage req, LuaTable? headers)
    {
        if (headers == null) return;
        foreach (var kvp in headers)
            if (kvp.Key.TryRead<string>(out var key) && kvp.Value.TryRead<string>(out var val))
                req.Headers.TryAddWithoutValidation(key, val);
    }

    private async Task<AudioData> ProcessReturnAsync(LuaValue result)
    {
        if (result.TryRead<string>(out var url))
        {
            var bytes = await _http.GetByteArrayAsync(url);
            return await ToAudioDataAsync(bytes, DetectFormat(url));
        }
        if (result.TryRead<LuaTable>(out var table))
        {
            if (table["url"].TryRead<string>(out var tableUrl))
            {
                table["format"].TryRead<string>(out var urlFmt);
                var bytes = await _http.GetByteArrayAsync(tableUrl);
                return await ToAudioDataAsync(bytes, urlFmt ?? DetectFormat(tableUrl));
            }
            if (table["data"].TryRead<string>(out var b64) && table["format"].TryRead<string>(out var fmt))
                return await ToAudioDataAsync(Convert.FromBase64String(b64), fmt);
        }
        return AudioData.Empty;
    }

    private static async Task<AudioData> ToAudioDataAsync(byte[] bytes, string format)
    {
        if (bytes.Length == 0) return AudioData.Empty;
        try
        {
            using var ms = new MemoryStream(bytes);
            WaveStream reader = format.Equals("wav", StringComparison.OrdinalIgnoreCase)
                ? (WaveStream)new WaveFileReader(ms)
                : new Mp3FileReader(ms);
            using var pcm = WaveFormatConversionStream.CreatePcmStream(reader);
            using var outMs = new MemoryStream();
            await pcm.CopyToAsync(outMs);
            return new AudioData { Samples = outMs.ToArray(), Format = pcm.WaveFormat };
        }
        catch { return AudioData.Empty; }
    }

    private static string DetectFormat(string url) =>
        url.Contains(".wav", StringComparison.OrdinalIgnoreCase) ? "wav" : "mp3";

    private static LuaValue JsonToLua(JToken token) => token switch
    {
        JObject obj => ObjectToLua(obj),
        JArray arr  => ArrayToLua(arr),
        JValue { Type: JTokenType.String }  v => (LuaValue)(v.Value<string>() ?? string.Empty),
        JValue { Type: JTokenType.Integer } v => (LuaValue)v.Value<double>(),
        JValue { Type: JTokenType.Float }   v => (LuaValue)v.Value<double>(),
        JValue { Type: JTokenType.Boolean } v => (LuaValue)v.Value<bool>(),
        _ => LuaValue.Nil
    };

    private static LuaValue ObjectToLua(JObject obj)
    {
        var t = new LuaTable();
        foreach (var prop in obj.Properties())
            t[prop.Name] = JsonToLua(prop.Value);
        return t;
    }

    private static LuaValue ArrayToLua(JArray arr)
    {
        var t = new LuaTable();
        for (var i = 0; i < arr.Count; i++)
            t[i + 1] = JsonToLua(arr[i]);
        return t;
    }

    private static JToken LuaToJson(LuaValue value)
    {
        if (value.TryRead<string>(out var s)) return new JValue(s);
        if (value.TryRead<double>(out var d)) return new JValue(d);
        if (value.TryRead<bool>(out var b)) return new JValue(b);
        if (value.TryRead<LuaTable>(out var t)) return LuaTableToJson(t);
        return JValue.CreateNull();
    }

    private static JToken LuaTableToJson(LuaTable table)
    {
        if (table.ArrayLength > 0)
        {
            var arr = new JArray();
            for (var i = 1; i <= table.ArrayLength; i++)
                arr.Add(LuaToJson(table[i]));
            return arr;
        }
        var obj = new JObject();
        foreach (var kvp in table)
            if (kvp.Key.TryRead<string>(out var key))
                obj[key] = LuaToJson(kvp.Value);
        return obj;
    }

    private static List<ExtSettingField> ParseSettingFields(LuaTable table)
    {
        var fields = new List<ExtSettingField>();
        for (var i = 1; i <= table.ArrayLength; i++)
        {
            if (!table[i].TryRead<LuaTable>(out var entry)) continue;
            if (!entry["key"].TryRead<string>(out var key)) continue;
            entry["label"].TryRead<string>(out var label);
            entry["type"].TryRead<string>(out var type);
            type ??= "text";

            string defaultStr;
            if (type == "checkbox")
            {
                defaultStr = entry["default"].TryRead<bool>(out var b) ? (b ? "true" : "false") : "false";
            }
            else if (type == "number")
            {
                defaultStr = entry["default"].TryRead<double>(out var n)
                    ? n.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : "0";
            }
            else
            {
                entry["default"].TryRead<string>(out defaultStr!);
                defaultStr ??= string.Empty;
            }

            var options = Array.Empty<string>();
            if (entry["options"].TryRead<LuaTable>(out var optTable))
            {
                var opts = new List<string>();
                for (var j = 1; j <= optTable.ArrayLength; j++)
                    if (optTable[j].TryRead<string>(out var opt))
                        opts.Add(opt);
                options = opts.ToArray();
            }

            fields.Add(new ExtSettingField(key, label ?? ToLabel(key), type, defaultStr, options));
        }
        return fields;
    }

    private static List<ExtAuthField> ParseAuthFields(LuaValue authVal)
    {
        var fields = new List<ExtAuthField>();
        if (!authVal.TryRead<LuaTable>(out var table)) return fields;
        for (var i = 1; i <= table.ArrayLength; i++)
        {
            if (!table[i].TryRead<LuaTable>(out var entry)) continue;
            string key, label, type;
            if (entry["key"].TryRead<string>(out var namedKey))
            {
                key = namedKey;
                label = entry["label"].TryRead<string>(out var l) ? l : ToLabel(key);
                type = entry["type"].TryRead<string>(out var tp) ? tp : "string";
            }
            else if (entry[1].TryRead<string>(out var pos1))
            {
                key = pos1;
                label = ToLabel(key);
                type = entry[2].TryRead<string>(out var pos2) ? pos2 : "string";
            }
            else continue;
            fields.Add(new ExtAuthField(key, label, type));
        }
        return fields;
    }

    private static string ToLabel(string key) =>
        key.Replace('_', ' ')
           .Split(' ', StringSplitOptions.RemoveEmptyEntries)
           .Select(w => char.ToUpper(w[0]) + w[1..])
           .Aggregate((a, b) => a + " " + b);

    private static string StripPrefix(string engineId) =>
        engineId.StartsWith("ext:", StringComparison.Ordinal) ? engineId[4..] : engineId;

    public void Dispose()
    {
        _http.Dispose();
        _stateLock.Dispose();
    }
}
