using System.IO;
using System.Text.Json;
using LiteDB;
using STJ = System.Text.Json;
using OpenSpeaker.Data;
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
        DatabaseContext db)
    {
        _settingsRepo = settingsRepo;
        _userRepo = userRepo;
        _aliasRepo = aliasRepo;
        _eventRepo = eventRepo;
        _rewardRepo = rewardRepo;
        _db = db;
    }

    public ImportResult Import(string folder)
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

        var settings  = STJ.JsonSerializer.Deserialize<SbSettings>(File.ReadAllText(settingsPath), JsonOpts)!;
        var aliases   = File.Exists(aliasesPath)
            ? STJ.JsonSerializer.Deserialize<SbAliasesFile>(File.ReadAllText(aliasesPath), JsonOpts)!
            : new SbAliasesFile();
        var usersFile = File.Exists(usersPath)
            ? STJ.JsonSerializer.Deserialize<SbUsersFile>(File.ReadAllText(usersPath), JsonOpts)!
            : new SbUsersFile();

        var aliasGuidToName = aliases.Aliases.ToDictionary(a => a.Id, a => a.Name, StringComparer.OrdinalIgnoreCase);

        var importedAliases = ImportAliases(aliases);
        var pastVoices = ReadPastVoices(pastVoicesPath, warnings);
        var importedUsers = ImportUsers(usersFile, aliasGuidToName, pastVoices);
        var importedEvents = ImportEvents(settings, aliasGuidToName);
        var importedRewards = ImportRewards(settings, aliasGuidToName);
        var importedEngines = ImportEngineConfigs(settings.TextToSpeech);
        var authImported = ImportTwitchAuth(Path.Combine(dataDir, "auth.db"), warnings);
        ImportSettings(settings, aliasGuidToName);

        return new ImportResult(importedUsers, importedAliases, importedEvents, importedRewards, importedEngines, authImported, warnings);
    }

    private int ImportAliases(SbAliasesFile aliasesFile)
    {
        var count = 0;
        foreach (var sbAlias in aliasesFile.Aliases)
        {
            var alias = _aliasRepo.GetByName(sbAlias.Name) ?? new VoiceAlias();
            alias.Name = sbAlias.Name;
            if (!string.IsNullOrEmpty(sbAlias.AudioDevice))
                alias.OutputDeviceId = sbAlias.AudioDevice;

            if (sbAlias.Voices.Count > 0)
            {
                var (engineId, voiceId) = ParseVoiceName(sbAlias.Voices[0].Name);
                alias.EngineId = engineId;
                alias.VoiceId  = voiceId;
                alias.Volume   = (int)Math.Round(sbAlias.Voices[0].Volume * 100);
                alias.VoiceIds = sbAlias.Voices
                    .Select(v => ParseVoiceName(v.Name).voiceId)
                    .Where(id => !string.IsNullOrEmpty(id))
                    .ToList();
            }

            _aliasRepo.Upsert(alias);
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
            warnings.Add("Could not read past_voices.db — past voice history was not imported.");
        }

        return result;
    }

    private int ImportUsers(SbUsersFile usersFile, Dictionary<string, string> aliasGuidToName, Dictionary<string, List<PastVoiceEntry>> pastVoices)
    {
        var count = 0;
        foreach (var (_, sbUser) in usersFile.Users)
        {
            var user = _userRepo.FindByTwitchId(sbUser.Id) ?? new UserRecord();
            user.TwitchId    = sbUser.Id;
            user.Username    = sbUser.Name;
            user.Nickname    = sbUser.Nickname ?? string.Empty;
            user.IsIgnored   = sbUser.Ignored;
            user.IsForced    = sbUser.Forced;
            user.IsRegular   = sbUser.Regular;
            user.IsSubscribed = sbUser.Subscribed;
            user.LastActive  = sbUser.LastActive;
            user.Role        = MapRole(sbUser.Role);

            if (!string.IsNullOrEmpty(sbUser.Voice) && aliasGuidToName.TryGetValue(sbUser.Voice, out var aliasName))
                user.AliasName = aliasName;

            if (pastVoices.TryGetValue(sbUser.Id, out var pv))
                user.PastVoices = pv;

            _userRepo.Upsert(user);
            count++;
        }
        return count;
    }

    private int ImportEvents(SbSettings settings, Dictionary<string, string> aliasGuidToName)
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
            count++;
        }
        return count;
    }

    private int ImportRewards(SbSettings settings, Dictionary<string, string> aliasGuidToName)
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
        ["acapela"]    = EngineIds.Acapela,
        ["cereproc"]   = EngineIds.CereProc,
    };

    private int ImportEngineConfigs(SbTextToSpeech tts)
    {
        var count = 0;
        foreach (var (sbKey, configElement) in tts.EngineConfig)
        {
            if (!SbEngineIdMap.TryGetValue(sbKey, out var engineId)) continue;

            var configJson = TranslateEngineConfig(engineId, configElement);
            var enabled = tts.EnabledEngines.Contains(sbKey, StringComparer.OrdinalIgnoreCase);

            var existing = _db.EngineConfigs.FindOne(c => c.EngineId == engineId);
            var config = existing ?? new EngineConfig { EngineId = engineId };
            config.Enabled = enabled;
            config.ConfigJson = configJson;
            _db.EngineConfigs.Upsert(config);
            count++;
        }
        return count;
    }

    private static string TranslateEngineConfig(string engineId, STJ.JsonElement src)
    {
        if (engineId == EngineIds.UberDuck)
        {
            var clientId = src.TryGetProperty("clientId", out var c) ? c.GetString() ?? string.Empty : string.Empty;
            var secret   = src.TryGetProperty("secretKey", out var s) ? s.GetString() ?? string.Empty : string.Empty;
            return STJ.JsonSerializer.Serialize(new { apiKey = clientId, apiSecret = secret });
        }

        return src.GetRawText();
    }

    private bool ImportTwitchAuth(string authDbPath, List<string> warnings)
    {
        if (!File.Exists(authDbPath)) return false;

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
                return true;
            }
        }
        catch
        {
            warnings.Add("Could not read auth.db — Twitch account was not imported. Re-authenticate in the Accounts tab.");
        }

        return false;
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
        s.StopOnUserTimedOut         = sb.StopAndSkipOnTimeout;
        s.StopOnUserBanned           = sb.StopAndSkipOnBan;
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
            var n when n.StartsWith("uberduck")   => EngineIds.UberDuck,
            var n when n.StartsWith("elevenlabs") => EngineIds.ElevenLabs,
            var n when n.StartsWith("ttsmonster") || n.StartsWith("tts monster") => EngineIds.TtsMonster,
            var n when n.StartsWith("amazon")     => EngineIds.AmazonPolly,
            var n when n.StartsWith("google")     => EngineIds.GoogleCloud,
            var n when n.StartsWith("azure")      => EngineIds.Azure,
            var n when n.StartsWith("ibm")        => EngineIds.IbmWatson,
            var n when n.StartsWith("acapela")    => EngineIds.Acapela,
            var n when n.StartsWith("cereproc")   => EngineIds.CereProc,
            _                                     => EngineIds.Sapi5,
        };

        var dashIdx = name.IndexOf(" - ", StringComparison.Ordinal);
        var voiceId = dashIdx >= 0 ? name[(dashIdx + 3)..].Trim() : name.Trim();

        return (engineId, voiceId);
    }

    private static string? GetStr(BsonDocument doc, string key) =>
        doc.ContainsKey(key) && !doc[key].IsNull ? doc[key].AsString : null;
}
