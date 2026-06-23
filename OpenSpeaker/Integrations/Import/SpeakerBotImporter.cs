using System.IO;
using System.Text.Json;
using LiteDB;
using STJ = System.Text.Json;
using OpenSpeaker.Data;
using OpenSpeaker.Infrastructure.Logging;
using OpenSpeaker.Models;
using OpenSpeaker.TTS;
namespace OpenSpeaker.Import;

public record ImportResult(
    int UsersImported,
    int AliasesImported,
    int EventsImported,
    int RewardsImported,
    int EnginesImported,
    bool AuthImported,
    List<string> Warnings);

public class SpeakerBotImporter
{
    private readonly SettingsRepository _settingsRepo;
    private readonly UserRepository _userRepo;
    private readonly VoiceAliasRepository _aliasRepo;
    private readonly EventConfigRepository _eventRepo;
    private readonly ChannelRewardRepository _rewardRepo;
    private readonly DatabaseContext _db;
    private readonly TtsEngineRegistry? _engineRegistry;
    private readonly IAppLogger? _logger;

    private static readonly STJ.JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private static readonly Dictionary<int, string> EventTypeMap = new()
    {
        [0]  = EventTypes.Follow,
        [1]  = EventTypes.Sub,
        [2]  = EventTypes.Resub,
        [3]  = EventTypes.Resub,
        [4]  = EventTypes.Resub,
        [5]  = EventTypes.GiftSub,
        [6]  = EventTypes.GiftSub,
        [8]  = EventTypes.Raid,
        [9]  = EventTypes.Cheer,
        [10] = EventTypes.Cheer,
        [12] = EventTypes.ChannelPoint,
        [13] = EventTypes.GiftBomb,
        [14] = EventTypes.GiftBomb,
        [15] = EventTypes.HypeTrain,
        [16] = EventTypes.HypeTrain,
        [17] = EventTypes.HypeTrain,
        [18] = EventTypes.Goal,
        [19] = EventTypes.Goal,
    };

    public SpeakerBotImporter(
        SettingsRepository settingsRepo,
        UserRepository userRepo,
        VoiceAliasRepository aliasRepo,
        EventConfigRepository eventRepo,
        ChannelRewardRepository rewardRepo,
        DatabaseContext db,
        TtsEngineRegistry? engineRegistry = null,
        IAppLogger? logger = null)
    {
        _settingsRepo = settingsRepo;
        _userRepo = userRepo;
        _aliasRepo = aliasRepo;
        _eventRepo = eventRepo;
        _rewardRepo = rewardRepo;
        _db = db;
        _engineRegistry = engineRegistry;
        _logger = logger;
    }

    public ImportResult Import(string folder, IProgress<string>? stageProgress = null, IProgress<string>? detailProgress = null, MigrationData? migration = null)
    {
        var warnings = new List<string>();

        var dataDir = Path.Combine(folder, "data");
        if (!Directory.Exists(dataDir) || !File.Exists(Path.Combine(dataDir, "settings.json")))
            dataDir = folder;

        var settingsPath   = Path.Combine(dataDir, "settings.json");
        var usersPath      = Path.Combine(dataDir, "users.dat");
        var aliasesPath    = Path.Combine(dataDir, "voicealiases.dat");
        var pastVoicesPath = Path.Combine(dataDir, "past_voices.db");

        if (!File.Exists(settingsPath))
            throw new FileNotFoundException("settings.json not found. Select the Speaker.bot install folder or its data subfolder.");

        stageProgress?.Report("Reading data files...");
        var settings  = STJ.JsonSerializer.Deserialize<SbSettings>(File.ReadAllText(settingsPath), JsonOpts)!;
        var aliases   = File.Exists(aliasesPath)
            ? STJ.JsonSerializer.Deserialize<SbAliasesFile>(File.ReadAllText(aliasesPath), JsonOpts)!
            : new SbAliasesFile();
        var usersFile = File.Exists(usersPath)
            ? STJ.JsonSerializer.Deserialize<SbUsersFile>(File.ReadAllText(usersPath), JsonOpts)!
            : new SbUsersFile();
        detailProgress?.Report($"settings.json, voicealiases.dat, users.dat");

        var aliasGuidToName = aliases.Aliases.ToDictionary(a => a.Id, a => a.Name, StringComparer.OrdinalIgnoreCase);

        stageProgress?.Report("Importing engine configs...");
        var importedEngines = ImportEngineConfigs(settings.TextToSpeech, migration, detailProgress);

        stageProgress?.Report($"Importing {aliases.Aliases.Count} voice aliases...");
        if (migration != null && migration.VoiceEngineRemap.Count > 0)
        {
            var remapStr = string.Join(", ", migration.VoiceEngineRemap.Select(k => $"{k.Key}→{k.Value}"));
            _logger?.Info($"[Import] Voice engine remaps active: {remapStr}");
            detailProgress?.Report($"Voice remaps: {remapStr}");
        }
        else
            _logger?.Info("[Import] No voice engine remaps (no migration hooks registered)");

        var voiceNameToId = BuildVoiceNameToIdCache(migration);
        var importedAliases = ImportAliases(aliases, migration, voiceNameToId, detailProgress);

        stageProgress?.Report("Reading voice history...");
        var pastVoices = ReadPastVoices(pastVoicesPath, warnings);
        detailProgress?.Report(pastVoices.Count > 0 ? $"Found history for {pastVoices.Count} users" : "No voice history found");

        stageProgress?.Report($"Importing {usersFile.Users.Count} users...");
        var importedUsers = ImportUsers(usersFile, aliasGuidToName, pastVoices, migration, detailProgress);

        stageProgress?.Report("Importing events...");
        var importedEvents = ImportEvents(settings, aliasGuidToName, detailProgress);

        stageProgress?.Report($"Importing {settings.ChannelPoints.Rewards.Count} channel rewards...");
        var importedRewards = ImportRewards(settings, aliasGuidToName, detailProgress);

        stageProgress?.Report("Importing Twitch account...");
        var authImported = ImportTwitchAuth(Path.Combine(dataDir, "auth.db"), warnings, detailProgress);

        stageProgress?.Report($"Importing {settings.CustomCommands.Commands.Count} custom commands...");
        ImportCustomCommands(settings, aliasGuidToName, detailProgress);

        stageProgress?.Report($"Importing {settings.Replacements.Count} replacements...");
        ImportReplacements(settings, detailProgress);

        stageProgress?.Report($"Importing {settings.BadWords.Count} bad words...");
        ImportBadWords(settings, detailProgress);

        stageProgress?.Report($"Importing {settings.IgnoredVoicesProfiles.Count} ignored voice profiles...");
        ImportIgnoredVoicesProfiles(settings, detailProgress);

        stageProgress?.Report("Applying general settings...");
        ImportSettings(settings, aliasGuidToName);
        detailProgress?.Report("Done");

        return new ImportResult(importedUsers, importedAliases, importedEvents, importedRewards, importedEngines, authImported, warnings);
    }

    private static readonly string[] BuiltInEnginesNeedingNameResolution = { EngineIds.ElevenLabs };

    private Dictionary<string, Dictionary<string, string>> BuildVoiceNameToIdCache(MigrationData? migration)
    {
        var cache = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (_engineRegistry == null)
        {
            _logger?.Warn($"[Import] BuildVoiceNameToIdCache: skipped (no engineRegistry)");
            return cache;
        }

        var targetEngineIds = (migration?.VoiceEngineRemap.Values
            .Where(v => v.StartsWith("ext:", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase) ?? Enumerable.Empty<string>())
            .Concat(BuiltInEnginesNeedingNameResolution)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _logger?.Info($"[Import] BuildVoiceNameToIdCache: fetching voices for: [{string.Join(", ", targetEngineIds)}]");

        foreach (var engineId in targetEngineIds)
        {
            var engine = _engineRegistry.GetEngine(engineId);
            if (engine == null)
            {
                _logger?.Warn($"[Import] BuildVoiceNameToIdCache: engine \"{engineId}\" not found in registry");
                continue;
            }
            try
            {
                var voices = engine.GetVoicesAsync().GetAwaiter().GetResult();
                _logger?.Info($"[Import] BuildVoiceNameToIdCache: got {voices.Count} voices from {engineId}");
                var nameToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var v in voices)
                {
                    _logger?.Info($"[Import]   voice id=\"{v.Id}\" name=\"{v.Name}\"");
                    if (!string.IsNullOrEmpty(v.Name) && !string.IsNullOrEmpty(v.Id))
                        nameToId[v.Name] = v.Id;
                }
                cache[engineId] = nameToId;
                _logger?.Info($"[Import] Built voice cache for {engineId}: {nameToId.Count} entries");
            }
            catch (Exception ex)
            {
                _logger?.Warn($"[Import] Could not fetch voices for {engineId}: {ex.Message}");
            }
        }

        return cache;
    }

    private int ImportAliases(SbAliasesFile aliasesFile, MigrationData? migration, Dictionary<string, Dictionary<string, string>> voiceNameToId, IProgress<string>? detail)
    {
        var count = 0;
        foreach (var sbAlias in aliasesFile.Aliases)
        {
            var alias = _aliasRepo.GetByName(sbAlias.Name) ?? new VoiceAlias();
            alias.Name = sbAlias.Name;
            if (!string.IsNullOrEmpty(sbAlias.AudioDevice))
                alias.OutputDeviceId = sbAlias.AudioDevice;

            _logger?.Info($"[Import] Alias \"{sbAlias.Name}\": {sbAlias.Voices.Count} voice(s) in SpeakerBot data");
            if (sbAlias.Voices.Count > 0)
            {
                var rawVoiceName = sbAlias.Voices[0].Name;
                _logger?.Info($"[Import] Alias \"{sbAlias.Name}\": raw voice name = \"{rawVoiceName}\"");
                var (engineId, voiceId) = ParseVoiceName(rawVoiceName);
                _logger?.Info($"[Import] Alias \"{sbAlias.Name}\": parsed engineId=\"{engineId}\" voiceId=\"{voiceId}\"");

                string? remappedEngineId = null;
                if (migration?.VoiceEngineRemap.TryGetValue(engineId, out remappedEngineId) == true)
                {
                    _logger?.Info($"[Import] Alias \"{sbAlias.Name}\": remapping engine {engineId} → {remappedEngineId}");
                    engineId = remappedEngineId!;
                }
                else
                    _logger?.Info($"[Import] Alias \"{sbAlias.Name}\": no engine remap for \"{engineId}\" (remap count={migration?.VoiceEngineRemap.Count ?? 0})");

                var resolveEngineId = remappedEngineId ?? engineId;
                if (voiceNameToId.TryGetValue(resolveEngineId, out var nameMap))
                {
                    _logger?.Info($"[Import] Alias \"{sbAlias.Name}\": resolving voice \"{voiceId}\" via {resolveEngineId} cache ({nameMap.Count} entries)");
                    if (nameMap.TryGetValue(voiceId, out var resolvedId))
                    {
                        _logger?.Info($"[Import] Alias \"{sbAlias.Name}\": resolved voice \"{voiceId}\" → \"{resolvedId}\"");
                        voiceId = resolvedId;
                    }
                    else
                        _logger?.Warn($"[Import] Alias \"{sbAlias.Name}\": no match for \"{voiceId}\" in {resolveEngineId} cache");
                }

                alias.EngineId = engineId;
                alias.VoiceId  = voiceId;
                alias.Volume   = (int)Math.Round(sbAlias.Voices[0].Volume * 100);
                alias.VoiceIds = sbAlias.Voices
                    .Select(v =>
                    {
                        var (_, vid) = ParseVoiceName(v.Name);
                        if (voiceNameToId.TryGetValue(resolveEngineId, out var nm) && nm.TryGetValue(vid, out var rid))
                            return rid;
                        return vid;
                    })
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Distinct()
                    .ToList();

                var sbVoice = sbAlias.Voices[0];
                var pitch = sbVoice.Pitch;
                var rate  = sbVoice.Rate;

                var engineParams = new Dictionary<string, string>();

                if (engineId == EngineIds.ElevenLabs)
                {
                    var model = ParseElevenLabsModel(sbAlias.Voices[0].Name);
                    if (model != null)
                        engineParams["model"] = model;
                    if (pitch != 0) engineParams["similarity_boost"] = (pitch / 100.0).ToString("G");
                    if (rate != 0)  engineParams["stability"]        = (rate  / 100.0).ToString("G");
                }
                else if (engineId is EngineIds.Sapi5 or EngineIds.Azure)
                {
                    if (pitch != 0) engineParams["pitch"] = pitch.ToString("G");
                    if (rate != 0)  engineParams["rate"]  = rate.ToString("G");
                }
                else if (engineId == EngineIds.CereProc)
                {
                    engineParams["pitch"] = pitch.ToString("G");
                    engineParams["speed"] = rate.ToString("G");
                }
                else if (engineId == EngineIds.GoogleCloud)
                {
                    if (pitch != 0) engineParams["pitch"]         = pitch.ToString("G");
                    if (rate != 0)  engineParams["speaking_rate"] = rate.ToString("G");
                }
                else if (engineId == EngineIds.Acapela)
                {
                    if (pitch != 0) engineParams["pitch"] = pitch.ToString("G");
                    if (rate != 0)  engineParams["rate"]  = rate.ToString("G");
                }

                if (engineParams.Count > 0)
                    alias.EngineParamsJson = STJ.JsonSerializer.Serialize(engineParams);
            }

            _aliasRepo.Upsert(alias);
            detail?.Report(sbAlias.Name);
            count++;
        }
        return count;
    }

    private static Dictionary<string, List<PastVoiceEntry>> ReadPastVoices(string path, List<string> warnings)
    {
        var result = new Dictionary<string, List<PastVoiceEntry>>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return result;

        try
        {
            using var db = new LiteDatabase($"Filename={path};ReadOnly=true");
            foreach (var colName in db.GetCollectionNames())
            {
                foreach (var doc in db.GetCollection(colName).FindAll())
                {
                    var userId  = GetStr(doc, "UserId") ?? GetStr(doc, "userId");
                    var voiceId = GetStr(doc, "VoiceId") ?? GetStr(doc, "voiceId");
                    if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(voiceId)) continue;

                    var engineId = GetStr(doc, "EngineId") ?? GetStr(doc, "engineId") ?? EngineIds.Sapi5;
                    var lastUsed = doc.ContainsKey("LastUsed") ? doc["LastUsed"].AsDateTime
                                 : doc.ContainsKey("lastUsed") ? doc["lastUsed"].AsDateTime
                                 : DateTime.UtcNow;

                    if (!result.ContainsKey(userId)) result[userId] = new();
                    result[userId].Add(new PastVoiceEntry { VoiceId = voiceId, EngineId = engineId, LastUsed = lastUsed });
                }
            }
        }
        catch
        {
            warnings.Add("Could not read past voice history.");
        }

        return result;
    }

    private int ImportUsers(SbUsersFile usersFile, Dictionary<string, string> aliasGuidToName, Dictionary<string, List<PastVoiceEntry>> pastVoices, MigrationData? migration, IProgress<string>? detail)
    {
        var total = usersFile.Users.Count;
        var batchSize = total > 500 ? 50 : total > 100 ? 25 : 1;

        var existing = _db.Users
            .FindAll()
            .Where(u => !string.IsNullOrEmpty(u.TwitchId))
            .ToDictionary(u => u.TwitchId, StringComparer.OrdinalIgnoreCase);

        var count = 0;
        var records = new List<UserRecord>(total);

        foreach (var (_, sbUser) in usersFile.Users)
        {
            existing.TryGetValue(sbUser.Id, out var user);
            user ??= new UserRecord();

            user.TwitchId     = sbUser.Id;
            user.Username     = sbUser.Name;
            user.Nickname     = sbUser.Nickname ?? string.Empty;
            user.IsIgnored    = sbUser.Ignored;
            user.IsForced     = sbUser.Forced;
            user.IsRegular    = sbUser.Regular;
            user.IsSubscribed = sbUser.Subscribed;
            user.LastActive   = sbUser.LastActive;
            user.Role         = MapRole(sbUser.Role);

            if (!string.IsNullOrEmpty(sbUser.Voice) && aliasGuidToName.TryGetValue(sbUser.Voice, out var aliasName))
                user.AliasName = aliasName;

            if (pastVoices.TryGetValue(sbUser.Id, out var pv))
                user.PastVoices = migration?.VoiceEngineRemap.Count > 0
                    ? pv.Select(e => migration.VoiceEngineRemap.TryGetValue(e.EngineId, out var r)
                        ? new PastVoiceEntry { VoiceId = e.VoiceId, EngineId = r, LastUsed = e.LastUsed }
                        : e).ToList()
                    : pv;

            records.Add(user);
            count++;

            if (detail != null && count % batchSize == 0)
                detail.Report($"[{count} / {total}] {sbUser.Name}");
        }

        if (detail != null && count % batchSize != 0)
            detail.Report($"[{count} / {total}] preparing bulk write...");

        _db.Users.Upsert(records);
        detail?.Report($"Wrote {count} users to database");

        return count;
    }

    private int ImportEvents(SbSettings settings, Dictionary<string, string> aliasGuidToName, IProgress<string>? detail)
    {
        var grouped = settings.Events.EventStrings
            .Where(e => EventTypeMap.ContainsKey(e.Type))
            .GroupBy(e => EventTypeMap[e.Type]);

        var count = 0;
        foreach (var group in grouped)
        {
            var eventType = group.Key;
            var config = _eventRepo.GetByEventType(eventType) ?? new EventConfig { EventType = eventType };

            config.Enabled = GetEventEnabled(settings.Events, eventType);
            config.State = new EventState
            {
                Enabled       = config.Enabled,
                MinRaidViewers = settings.Events.RaidMinRaiders,
                MinBits       = settings.Events.CheerMinAmount,
            };

            config.Messages = group.Select(e => new EventMessage
            {
                Template = ConvertTemplate(e.Template, eventType),
                Weight   = Math.Max(1, (int)Math.Round(e.Weight == 0 ? 1 : e.Weight)),
                Enabled  = e.Enabled,
            }).ToList();

            if (!string.IsNullOrEmpty(settings.Events.Voice) &&
                aliasGuidToName.TryGetValue(settings.Events.Voice, out var voiceAlias))
                config.VoiceAliasOverride = voiceAlias;

            _eventRepo.Upsert(config);
            detail?.Report($"{eventType} ({config.Messages.Count} messages, {(config.Enabled ? "enabled" : "disabled")})");
            count++;
        }
        return count;
    }

    private int ImportRewards(SbSettings settings, Dictionary<string, string> aliasGuidToName, IProgress<string>? detail)
    {
        const string nullGuid = "00000000-0000-0000-0000-000000000000";
        var count = 0;

        foreach (var sbReward in settings.ChannelPoints.Rewards)
        {
            var reward = _rewardRepo.GetByTwitchId(sbReward.Id) ?? new ChannelReward();
            reward.TwitchRewardId = sbReward.Id;
            reward.Title          = sbReward.Name;
            reward.Cost           = sbReward.Cost;
            reward.IsIgnored      = sbReward.Ignored;
            reward.SayInput       = sbReward.SayInput;
            reward.Messages       = sbReward.Messages.Select(m => new EventMessage
            {
                Template = m.Template,
                Weight   = Math.Max(1, (int)Math.Round(m.Weight == 0 ? 1 : m.Weight)),
                Enabled  = m.Enabled,
            }).ToList();

            if (!string.IsNullOrEmpty(sbReward.Voice) && sbReward.Voice != nullGuid &&
                aliasGuidToName.TryGetValue(sbReward.Voice, out var rewardAlias))
                reward.VoiceAliasName = rewardAlias;

            _rewardRepo.Upsert(reward);
            detail?.Report($"{sbReward.Name} ({sbReward.Cost} pts)");
            count++;
        }
        return count;
    }

    private static readonly Dictionary<string, string> SbEngineIdMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["uberduck"]   = EngineIds.UberDuck,
        ["elevenlabs"] = EngineIds.ElevenLabs,
        ["ttsmonster"] = EngineIds.TtsMonster,
        ["azure"]      = EngineIds.Azure,
        ["amazon"]     = EngineIds.AmazonPolly,
        ["amazonpolly"]= EngineIds.AmazonPolly,
        ["google"]     = EngineIds.GoogleCloud,
        ["googlecloud"]= EngineIds.GoogleCloud,
        ["ibmwatson"]  = EngineIds.IbmWatson,
        ["ibm"]        = EngineIds.IbmWatson,
        ["watson"]     = EngineIds.IbmWatson,
        ["acapela"]    = EngineIds.Acapela,
        ["cereproc"]   = EngineIds.CereProc,
        ["cerevoice"]  = EngineIds.CereProc,
    };

    private int ImportEngineConfigs(SbTextToSpeech tts, MigrationData? migration, IProgress<string>? detail)
    {
        var count = 0;
        foreach (var (sbKey, configElement) in tts.EngineConfig)
        {
            if (migration?.EngineConfigs.TryGetValue(sbKey, out var migCfg) == true)
            {
                var obj = new STJ.Nodes.JsonObject();
                foreach (var (targetField, sourceField) in migCfg.FieldMap)
                    if (configElement.TryGetProperty(sourceField, out var val))
                        obj[targetField] = val.GetString();
                var existing = _db.EngineConfigs.FindOne(c => c.EngineId == migCfg.TargetEngineId);
                var cfg = existing ?? new EngineConfig { EngineId = migCfg.TargetEngineId };
                cfg.Enabled    = true;
                cfg.ConfigJson = obj.ToJsonString();
                _db.EngineConfigs.Upsert(cfg);
                detail?.Report($"{migCfg.TargetEngineId} (migrated from {sbKey})");
                count++;
                continue;
            }

            if (!SbEngineIdMap.TryGetValue(sbKey, out var engineId)) continue;

            var (configJson, warning) = TranslateEngineConfig(engineId, configElement);
            if (warning != null)
            {
                detail?.Report($"Skipped {engineId}: {warning}");
                continue;
            }

            var enabled = tts.EnabledEngines.Contains(sbKey, StringComparer.OrdinalIgnoreCase);

            var existing2 = _db.EngineConfigs.FindOne(c => c.EngineId == engineId);
            var config = existing2 ?? new EngineConfig { EngineId = engineId };
            config.Enabled = enabled;
            config.ConfigJson = configJson;
            _db.EngineConfigs.Upsert(config);
            detail?.Report($"{engineId} ({(enabled ? "enabled" : "disabled")})");
            count++;
        }
        return count;
    }

    private static (string configJson, string? warning) TranslateEngineConfig(string engineId, STJ.JsonElement src)
    {
        string Str(string key) => src.TryGetProperty(key, out var v) ? v.GetString() ?? string.Empty : string.Empty;

        if (engineId == EngineIds.UberDuck)
            return (STJ.JsonSerializer.Serialize(new { apiKey = Str("clientId"), apiSecret = Str("secretKey") }), null);

        if (engineId == EngineIds.AmazonPolly)
            return (STJ.JsonSerializer.Serialize(new
            {
                accessKey = Str("accessKeyId"),
                secretKey = Str("secretAccessKey"),
                region    = Str("region") is { Length: > 0 } r ? r : "us-east-1",
            }), null);

        if (engineId == EngineIds.Azure)
            return (STJ.JsonSerializer.Serialize(new
            {
                subscriptionKey = Str("subscriptionKey"),
                region          = Str("serviceRegion") is { Length: > 0 } r ? r : Str("region"),
            }), null);

        if (engineId == EngineIds.CereProc)
            return (STJ.JsonSerializer.Serialize(new
            {
                username = Str("email") is { Length: > 0 } e ? e : Str("username"),
                password = Str("password"),
            }), null);

        if (engineId == EngineIds.GoogleCloud)
            return (STJ.JsonSerializer.Serialize(new
            {
                serviceAccountJsonPath = Str("credentialsFile") is { Length: > 0 } f ? f : Str("serviceAccountJsonPath"),
            }), null);

        if (engineId == EngineIds.IbmWatson)
            return (STJ.JsonSerializer.Serialize(new
            {
                envFilePath = Str("credentialsFile") is { Length: > 0 } f ? f : Str("envFilePath"),
            }), null);

        if (engineId == EngineIds.TtsMonster)
        {
            var userId = Str("userId");
            var key    = Str("key") is { Length: > 0 } k ? k : Str("apiKey") is { Length: > 0 } ak ? ak : Str("overlayKey");
            if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(key))
                return (STJ.JsonSerializer.Serialize(new { overlayUrl = $"https://tts.monster/overlay/{userId}/{key}" }), null);

            var overlayUrl = Str("overlayUrl");
            if (!string.IsNullOrEmpty(overlayUrl))
                return (STJ.JsonSerializer.Serialize(new { overlayUrl }), null);

            var apiToken = Str("apiToken");
            if (!string.IsNullOrEmpty(apiToken))
                return (STJ.JsonSerializer.Serialize(new { apiToken }), null);

            return (string.Empty, "TTS Monster credentials not found in SpeakerBot settings - paste your overlay URL manually in engine settings");
        }

        return (src.GetRawText(), null);
    }

    private bool ImportTwitchAuth(string authDbPath, List<string> warnings, IProgress<string>? detail)
    {
        if (!File.Exists(authDbPath))
        {
            detail?.Report("auth.db not found, skipping");
            return false;
        }

        try
        {
            using var authDb = new LiteDatabase($"Filename={authDbPath};ReadOnly=true");
            foreach (var colName in authDb.GetCollectionNames())
            {
                var doc = authDb.GetCollection(colName).FindOne(Query.All());
                if (doc == null) continue;

                var accessToken  = GetStr(doc, "accessToken") ?? GetStr(doc, "AccessToken") ?? string.Empty;
                var refreshToken = GetStr(doc, "refreshToken") ?? GetStr(doc, "RefreshToken") ?? string.Empty;
                var userId       = GetStr(doc, "userId") ?? GetStr(doc, "UserId") ?? string.Empty;
                var login        = GetStr(doc, "login") ?? GetStr(doc, "Login") ?? string.Empty;
                var displayName  = GetStr(doc, "displayName") ?? GetStr(doc, "DisplayName") ?? string.Empty;
                var clientId     = GetStr(doc, "clientId") ?? GetStr(doc, "ClientId") ?? string.Empty;

                if (string.IsNullOrEmpty(userId) && string.IsNullOrEmpty(login)) continue;

                var account = new TwitchAccountInfo
                {
                    AccessToken  = accessToken,
                    RefreshToken = refreshToken,
                    UserId       = userId,
                    Login        = login,
                    DisplayName  = displayName,
                    ClientId     = clientId,
                };
                _db.TwitchAccounts.Upsert(account);
                detail?.Report($"Imported account: {displayName} ({login})");
                return true;
            }
        }
        catch
        {
            warnings.Add("Could not read Twitch authentication. Re-authenticate in the Accounts tab.");
        }

        return false;
    }

    private void ImportCustomCommands(SbSettings sb, Dictionary<string, string> aliasGuidToName, IProgress<string>? detail)
    {
        _db.CustomCommands.DeleteAll();
        var roles = SbPermissionsToRoles(sb.CustomCommands.Permissions);
        foreach (var c in sb.CustomCommands.Commands)
        {
            if (string.IsNullOrWhiteSpace(c.Command)) continue;
            aliasGuidToName.TryGetValue(c.Voice, out var aliasName);
            _db.CustomCommands.Insert(new CustomCommand
            {
                Trigger        = c.Command,
                VoiceAliasName = aliasName ?? string.Empty,
                AllowedRoles   = roles.ToList(),
                Enabled        = true,
            });
            detail?.Report(c.Command);
        }
    }

    private static List<string> SbPermissionsToRoles(int perms)
    {
        var roles = new List<string>();
        if ((perms & 1)  != 0) roles.Add(UserRoles.Everyone);
        if ((perms & 2)  != 0) roles.Add(UserRoles.Subscriber);
        if ((perms & 4)  != 0) roles.Add(UserRoles.VIP);
        if ((perms & 8)  != 0) roles.Add(UserRoles.Moderator);
        if (roles.Count == 0)  roles.Add(UserRoles.Moderator);
        return roles;
    }

    private void ImportReplacements(SbSettings sb, IProgress<string>? detail)
    {
        _db.RegexReplacements.DeleteAll();
        var order = 0;
        foreach (var r in sb.Replacements)
        {
            _db.RegexReplacements.Insert(new RegexReplacement
            {
                Pattern     = r.Pattern,
                Replacement = r.With,
                Enabled     = r.Enabled,
                Order       = order++,
            });
        }
        detail?.Report($"Imported {sb.Replacements.Count} replacements");
    }

    private void ImportBadWords(SbSettings sb, IProgress<string>? detail)
    {
        var yeetAll = sb.BadWordFilter == 2;
        var nextOrder = _db.RegexReplacements.Count();
        foreach (var w in sb.BadWords.Where(w => !string.IsNullOrWhiteSpace(w)))
        {
            _db.RegexReplacements.Insert(new RegexReplacement
            {
                Pattern     = w,
                Replacement = yeetAll ? string.Empty : "***",
                IsRegex     = false,
                WholeWord   = true,
                Mode        = yeetAll ? "Skip" : "Replace",
                Enabled     = true,
                Order       = nextOrder++,
            });
        }
        detail?.Report($"Imported {sb.BadWords.Count} bad words ({(yeetAll ? "skip message" : "replace with ***")})");
    }

    private void ImportIgnoredVoicesProfiles(SbSettings sb, IProgress<string>? detail)
    {
        _db.IgnoreProfiles.DeleteAll();
        foreach (var p in sb.IgnoredVoicesProfiles)
        {
            var voiceIds = p.Voices.Select(SbIgnoredVoiceToId).ToList();
            _db.IgnoreProfiles.Insert(new IgnoreProfile
            {
                Name             = string.IsNullOrEmpty(p.Name) ? "Default" : p.Name,
                ExcludedVoiceIds = voiceIds,
                ExcludedLocales  = p.Locales.ToList(),
                IsActive         = p.Id == sb.IgnoredVoicesProfile,
            });
        }
        detail?.Report($"Imported {sb.IgnoredVoicesProfiles.Count} ignored voice profiles");
    }

    private static string SbIgnoredVoiceToId(string sbVoice)
    {
        var open  = sbVoice.LastIndexOf('(');
        var close = sbVoice.LastIndexOf(')');
        if (open <= 0 || close <= open) return $"sapi5::{sbVoice}";

        var name      = sbVoice[..open].Trim();
        var sbEngine  = sbVoice[(open + 1)..close].Trim();
        var engineId  = sbEngine.ToLowerInvariant() switch
        {
            "sapi5"        => EngineIds.Sapi5,
            "uberduck"     => EngineIds.UberDuck,
            "elevenlabs"   => EngineIds.ElevenLabs,
            "elevenlabs.io"=> EngineIds.ElevenLabs,
            "tts.monster"  => EngineIds.TtsMonster,
            "amazon"       => EngineIds.AmazonPolly,
            "google"       => EngineIds.GoogleCloud,
            "azure"        => EngineIds.Azure,
            "ibm"          => EngineIds.IbmWatson,
            "watson"       => EngineIds.IbmWatson,
            "cereproc"     => EngineIds.CereProc,
            _              => EngineIds.Sapi5,
        };
        return $"{engineId}::{name}";
    }

    private void ImportSettings(SbSettings sb, Dictionary<string, string> aliasGuidToName)
    {
        var s = _settingsRepo.GetSettings();

        s.Enabled                    = sb.SpeakingEnabled;
        s.Mode                       = sb.SayEverything ? TtsModes.Everything : TtsModes.Command;
        s.SayUsername                = sb.SayUserName;
        s.SayUsernamePrefix          = sb.UserSaidPostfix;
        s.OnlySayUsernameIfDifferent = sb.SayUsernameLastDifferent;
        s.ReplaceNameWithNickname    = sb.SwapNameWithNickname;
        s.StickyRandomVoice          = sb.StickyRandomVoice;
        s.ApplicationVolume          = Math.Clamp((int)Math.Round(sb.Volume * 100), 0, 100);
        s.SaveTts                    = sb.SaveAudio;
        s.SaveTtsFolder              = sb.AudioFolder;
        s.StripTwitchEmotes          = sb.IgnoreTwitchEmotes;
        s.StripBttvEmotes            = sb.IgnoreBttvEmotes;
        s.StripFfzEmotes             = sb.IgnoreFfzEmotes;
        s.StripSevenTvEmotes         = sb.Ignore7TvEmotes;
        s.StripTwemoji               = sb.IgnoreTwemojiEmotes;
        s.StripCheermotes            = sb.IgnoreCheerEmotes;
        s.AllowFirstEmote            = sb.AllowFirstEmote;
        s.AllowedEmotes              = sb.AllowedEmotes;
        s.NotAllowedText             = sb.NotAllowedText;
        s.UrlFilterMode              = sb.UrlFilter switch { 1 => "Strip", 2 => "Block", _ => "Disabled" };
        s.MaxWords                   = sb.MaxWords;
        s.SilenceCommandOutput       = sb.SilenceCommandOutput;
        s.TtsCommands                = sb.SpeakingCommands;
        s.IgnoredPrefixes            = sb.IgnorePrefixes;
        s.UseHighlightVoice          = sb.HighlightVoiceOverride;
        s.StopOnMessageDeleted       = sb.StopAndSkipOnMessageDeleted;
        s.SkipOnMessageDeleted       = sb.StopAndSkipOnMessageDeleted;
        s.StopOnUserTimedOut         = sb.StopAndSkipOnTimeout;
        s.SkipOnUserTimedOut         = sb.StopAndSkipOnTimeout;
        s.StopOnUserBanned           = sb.StopAndSkipOnBan;
        s.SkipOnUserBanned           = sb.StopAndSkipOnBan;
        s.EventsEnabled              = sb.Events.Speak;
        s.MinimizeToTray             = sb.WindowSettings.MinimizeToTray;
        s.ConfirmationOnClose        = sb.WindowSettings.CloseConfirmation;
        s.LogLevel                   = sb.LogLevel switch { 0 => "Debug", 1 => "Info", 2 => "Warn", _ => "Error" };
        s.AllowEveryone              = (sb.Permissions & 1) != 0;
        s.AllowSubscribers           = (sb.Permissions & 2) != 0;
        s.AllowVIPs                  = (sb.Permissions & 4) != 0;
        s.AllowModerators            = (sb.Permissions & 8) != 0;
        s.WebSocketServer.AutoStart  = sb.Websockets.AutoStart;
        s.WebSocketServer.Address    = sb.Websockets.Address;
        s.WebSocketServer.Port       = sb.Websockets.Port;
        s.WebSocketServer.Endpoint   = sb.Websockets.Endpoint;

        if (!string.IsNullOrEmpty(sb.InstanceName))
            s.InstanceName = sb.InstanceName;

        if (!string.IsNullOrEmpty(sb.DefaultVoice) && aliasGuidToName.TryGetValue(sb.DefaultVoice, out var defaultAlias))
            s.DefaultVoiceAlias = defaultAlias;

        if (!string.IsNullOrEmpty(sb.HighlightVoice) && aliasGuidToName.TryGetValue(sb.HighlightVoice, out var highlightAlias))
            s.HighlightVoiceAlias = highlightAlias;

        _settingsRepo.SaveSettings(s);
    }

    private static string MapRole(int role) => role switch
    {
        4 => UserRoles.Broadcaster,
        3 => UserRoles.Moderator,
        2 => UserRoles.VIP,
        1 => UserRoles.Everyone,
        _ => UserRoles.Everyone,
    };

    private static bool GetEventEnabled(SbEventsSettings e, string eventType) => eventType switch
    {
        EventTypes.Follow       => e.Follows,
        EventTypes.Sub          => e.Subscriptions,
        EventTypes.Resub        => e.Subscriptions,
        EventTypes.GiftSub      => e.Subscriptions,
        EventTypes.GiftBomb     => e.GiftBomb,
        EventTypes.Raid         => e.Raids,
        EventTypes.Cheer        => e.Cheers,
        EventTypes.ChannelPoint => e.ChannelPoints,
        EventTypes.HypeTrain    => e.HypeTrain,
        EventTypes.Goal         => e.CommunityGoal,
        _                       => false,
    };

    private static string ConvertTemplate(string template, string eventType)
    {
        var t = template
            .Replace("%viewers%", "%amount%")
            .Replace("%gifts%", "%gift%")
            .Replace("%subtier%", "%tier%")
            .Replace("%product%", "%title%")
            .Replace("%totalgifts%", "%gift%");
        return t;
    }

    private static (string engineId, string voiceId) ParseVoiceName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return (EngineIds.Sapi5, string.Empty);

        var engineId = name.ToLowerInvariant() switch
        {
            var n when n.StartsWith("uberduck")                          => EngineIds.UberDuck,
            var n when n.StartsWith("elevenlabs")                        => EngineIds.ElevenLabs,
            var n when n.StartsWith("tts.monster") || n.StartsWith("ttsmonster") || n.StartsWith("tts monster") => EngineIds.TtsMonster,
            var n when n.StartsWith("amazon")                            => EngineIds.AmazonPolly,
            var n when n.StartsWith("google")                            => EngineIds.GoogleCloud,
            var n when n.StartsWith("azure")                             => EngineIds.Azure,
            var n when n.StartsWith("ibm") || n.StartsWith("watson")    => EngineIds.IbmWatson,
            var n when n.StartsWith("acapela")                           => EngineIds.Acapela,
            var n when n.StartsWith("cereproc")                          => EngineIds.CereProc,
            _                                                            => EngineIds.Sapi5,
        };

        var dashIdx = name.IndexOf(" - ", StringComparison.Ordinal);
        var raw = dashIdx >= 0 ? name[(dashIdx + 3)..].Trim() : name.Trim();

        var voiceId = engineId switch
        {
            EngineIds.AmazonPolly => ExtractPollyVoiceId(raw),
            EngineIds.Azure       => ExtractAzureShortName(raw),
            EngineIds.IbmWatson   => raw.Split(" - ")[0].Trim(),
            EngineIds.GoogleCloud => ExtractGoogleVoiceId(raw),
            EngineIds.CereProc    => raw.Split(" - ")[0].Trim(),
            EngineIds.ElevenLabs  => raw.LastIndexOf('(') is > 0 and var p ? raw[..p].Trim() : raw,
            EngineIds.UberDuck    => ExtractUberDuckDisplayName(name),
            _                     => raw,
        };

        return (engineId, voiceId);
    }

    private static string ExtractUberDuckDisplayName(string fullName)
    {
        var parts = fullName.Split(" - ");
        return parts.Length >= 3 ? parts[2].Trim() : (parts.Length >= 2 ? parts[1].Trim() : fullName.Trim());
    }

    private static string ExtractPollyVoiceId(string raw)
    {
        var lastDash = raw.LastIndexOf(" - ", StringComparison.Ordinal);
        var segment  = lastDash >= 0 ? raw[(lastDash + 3)..] : raw;
        var paren    = segment.IndexOf(" (", StringComparison.Ordinal);
        return (paren >= 0 ? segment[..paren] : segment).Trim();
    }

    private static string ExtractAzureShortName(string raw)
    {
        var open  = raw.LastIndexOf('(');
        var close = raw.LastIndexOf(')');
        if (open < 0 || close <= open) return raw;
        return raw[(open + 1)..close].Replace(", ", "-").Trim();
    }

    private static string ExtractGoogleVoiceId(string raw)
    {
        var lastDash  = raw.LastIndexOf(" - ", StringComparison.Ordinal);
        var voiceName = lastDash >= 0 ? raw[..lastDash].Trim() : raw.Trim();
        var locale    = voiceName.Length >= 5 ? voiceName[..5] : "en-US";
        return $"{voiceName}|{locale}";
    }

    private static readonly Dictionary<string, string> ElevenLabsModelMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["v3"]              = "eleven_v3",
        ["turbo v2.5"]      = "eleven_turbo_v2_5",
        ["turbo v2"]        = "eleven_turbo_v2",
        ["flash v2.5"]      = "eleven_flash_v2_5",
        ["flash v2"]        = "eleven_flash_v2",
        ["multilingual v2"] = "eleven_multilingual_v2",
        ["multilingual v1"] = "eleven_multilingual_v1",
        ["english v1"]      = "eleven_monolingual_v1",
    };

    private static string? ParseElevenLabsModel(string sbVoiceName)
    {
        var open  = sbVoiceName.LastIndexOf('(');
        var close = sbVoiceName.LastIndexOf(')');
        if (open < 0 || close <= open) return null;
        var label = sbVoiceName[(open + 1)..close];
        return ElevenLabsModelMap.TryGetValue(label, out var id) ? id : null;
    }

    private static string? GetStr(BsonDocument doc, string key) =>
        doc.ContainsKey(key) && !doc[key].IsNull ? doc[key].AsString : null;
}
