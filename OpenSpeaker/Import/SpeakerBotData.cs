using System.Text.Json.Serialization;
namespace OpenSpeaker.Import;

public class SbTextToSpeech
{
    [JsonPropertyName("enabledEngines")] public List<string> EnabledEngines { get; set; } = new();
    [JsonPropertyName("engineConfig")] public Dictionary<string, System.Text.Json.JsonElement> EngineConfig { get; set; } = new();
}

public class SbCustomCommand
{
    [JsonPropertyName("command")] public string Command { get; set; } = string.Empty;
    [JsonPropertyName("voice")]   public string Voice   { get; set; } = string.Empty;
}

public class SbCustomCommands
{
    [JsonPropertyName("commands")]    public List<SbCustomCommand> Commands    { get; set; } = new();
    [JsonPropertyName("permissions")] public int                   Permissions { get; set; } = 31;
}

public class SbReplacement
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("replace")] public string Pattern { get; set; } = string.Empty;
    [JsonPropertyName("with")] public string With { get; set; } = string.Empty;
}

public class SbIgnoredVoicesProfile
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("voices")] public List<string> Voices { get; set; } = new();
    [JsonPropertyName("locales")] public List<string> Locales { get; set; } = new();
}

public class SbSettings
{
    [JsonPropertyName("speakingEnabled")] public bool SpeakingEnabled { get; set; }
    [JsonPropertyName("sayEverything")] public bool SayEverything { get; set; } = true;
    [JsonPropertyName("sayUserName")] public bool SayUserName { get; set; }
    [JsonPropertyName("userSaidPostfix")] public string UserSaidPostfix { get; set; } = "said";
    [JsonPropertyName("sayUsernameLastDifferent")] public bool SayUsernameLastDifferent { get; set; }
    [JsonPropertyName("swapNameWithNickname")] public bool SwapNameWithNickname { get; set; }
    [JsonPropertyName("stickyRandomVoice")] public bool StickyRandomVoice { get; set; }
    [JsonPropertyName("volume")] public double Volume { get; set; } = 1.0;
    [JsonPropertyName("saveAudio")] public bool SaveAudio { get; set; }
    [JsonPropertyName("audioFolder")] public string AudioFolder { get; set; } = string.Empty;
    [JsonPropertyName("ignoreTwitchEmotes")] public bool IgnoreTwitchEmotes { get; set; }
    [JsonPropertyName("ignoreBttvEmotes")] public bool IgnoreBttvEmotes { get; set; }
    [JsonPropertyName("ignoreFfzEmotes")] public bool IgnoreFfzEmotes { get; set; }
    [JsonPropertyName("ignore7TvEmotes")] public bool Ignore7TvEmotes { get; set; }
    [JsonPropertyName("ignoreTwemojiEmotes")] public bool IgnoreTwemojiEmotes { get; set; }
    [JsonPropertyName("ignoreCheerEmotes")] public bool IgnoreCheerEmotes { get; set; }
    [JsonPropertyName("allowFirstEmote")] public bool AllowFirstEmote { get; set; }
    [JsonPropertyName("allowedEmotes")] public List<string> AllowedEmotes { get; set; } = new();
    [JsonPropertyName("notAllowedText")] public string NotAllowedText { get; set; } = string.Empty;
    [JsonPropertyName("urlFilter")] public int UrlFilter { get; set; }
    [JsonPropertyName("maxWords")] public int MaxWords { get; set; }
    [JsonPropertyName("silenceCommandOutput")] public bool SilenceCommandOutput { get; set; }
    [JsonPropertyName("permissions")] public int Permissions { get; set; } = 31;
    [JsonPropertyName("speakingCommands")] public List<string> SpeakingCommands { get; set; } = new();
    [JsonPropertyName("ignorePrefixes")] public List<string> IgnorePrefixes { get; set; } = new();
    [JsonPropertyName("highlightVoiceOverride")] public bool HighlightVoiceOverride { get; set; }
    [JsonPropertyName("stopAndSkipOnMessageDeleted")] public bool StopAndSkipOnMessageDeleted { get; set; }
    [JsonPropertyName("stopAndSkipOnTimeout")] public bool StopAndSkipOnTimeout { get; set; }
    [JsonPropertyName("stopAndSkipOnBan")] public bool StopAndSkipOnBan { get; set; }
    [JsonPropertyName("logLevel")] public int LogLevel { get; set; }
    [JsonPropertyName("events")] public SbEventsSettings Events { get; set; } = new();
    [JsonPropertyName("channelPoints")] public SbChannelPoints ChannelPoints { get; set; } = new();
    [JsonPropertyName("windowSettings")] public SbWindowSettings WindowSettings { get; set; } = new();
    [JsonPropertyName("websockets")] public SbWebSocketSettings Websockets { get; set; } = new();
    [JsonPropertyName("instanceName")] public string InstanceName { get; set; } = string.Empty;
    [JsonPropertyName("textToSpeech")] public SbTextToSpeech TextToSpeech { get; set; } = new();
    [JsonPropertyName("customCommands")] public SbCustomCommands CustomCommands { get; set; } = new();
    [JsonPropertyName("replacements")] public List<SbReplacement> Replacements { get; set; } = new();
    [JsonPropertyName("badWords")] public List<string> BadWords { get; set; } = new();
    [JsonPropertyName("badWordFilter")] public int BadWordFilter { get; set; }
    [JsonPropertyName("ignoredVoicesProfiles")] public List<SbIgnoredVoicesProfile> IgnoredVoicesProfiles { get; set; } = new();
    [JsonPropertyName("ignoredVoicesProfile")] public string IgnoredVoicesProfile { get; set; } = string.Empty;
    [JsonPropertyName("defaultVoice")] public string DefaultVoice { get; set; } = string.Empty;
    [JsonPropertyName("highlightVoice")] public string HighlightVoice { get; set; } = string.Empty;
}

public class SbEventsSettings
{
    [JsonPropertyName("speak")] public bool Speak { get; set; }
    [JsonPropertyName("follows")] public bool Follows { get; set; }
    [JsonPropertyName("subscriptions")] public bool Subscriptions { get; set; }
    [JsonPropertyName("giftBomb")] public bool GiftBomb { get; set; }
    [JsonPropertyName("raids")] public bool Raids { get; set; }
    [JsonPropertyName("raidMinRaiders")] public int RaidMinRaiders { get; set; }
    [JsonPropertyName("cheers")] public bool Cheers { get; set; }
    [JsonPropertyName("cheerMinAmount")] public int CheerMinAmount { get; set; }
    [JsonPropertyName("channelPoints")] public bool ChannelPoints { get; set; }
    [JsonPropertyName("hypeTrain")] public bool HypeTrain { get; set; }
    [JsonPropertyName("communityGoal")] public bool CommunityGoal { get; set; }
    [JsonPropertyName("voice")] public string? Voice { get; set; }
    [JsonPropertyName("eventStrings")] public List<SbEventString> EventStrings { get; set; } = new();
}

public class SbEventString
{
    [JsonPropertyName("type")] public int Type { get; set; }
    [JsonPropertyName("weight")] public double Weight { get; set; }
    [JsonPropertyName("string")] public string Template { get; set; } = string.Empty;
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
}

public class SbChannelPoints
{
    [JsonPropertyName("rewards")] public List<SbReward> Rewards { get; set; } = new();
}

public class SbReward
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("cost")] public int Cost { get; set; }
    [JsonPropertyName("ignored")] public bool Ignored { get; set; }
    [JsonPropertyName("sayInput")] public bool SayInput { get; set; }
    [JsonPropertyName("voice")] public string? Voice { get; set; }
    [JsonPropertyName("messages")] public List<SbRewardMessage> Messages { get; set; } = new();
}

public class SbRewardMessage
{
    [JsonPropertyName("string")] public string Template { get; set; } = string.Empty;
    [JsonPropertyName("weight")] public double Weight { get; set; }
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
}

public class SbWindowSettings
{
    [JsonPropertyName("minimizeToTray")] public bool MinimizeToTray { get; set; }
    [JsonPropertyName("closeConfirmation")] public bool CloseConfirmation { get; set; }
}

public class SbWebSocketSettings
{
    [JsonPropertyName("autoStart")] public bool AutoStart { get; set; }
    [JsonPropertyName("address")] public string Address { get; set; } = "127.0.0.1";
    [JsonPropertyName("port")] public int Port { get; set; } = 7680;
    [JsonPropertyName("endpoint")] public string Endpoint { get; set; } = "/";
}

public class SbUsersFile
{
    [JsonPropertyName("users")] public Dictionary<string, SbUser> Users { get; set; } = new();
}

public class SbUser
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("role")] public int Role { get; set; }
    [JsonPropertyName("subscribed")] public bool Subscribed { get; set; }
    [JsonPropertyName("regular")] public bool Regular { get; set; }
    [JsonPropertyName("forced")] public bool Forced { get; set; }
    [JsonPropertyName("ignored")] public bool Ignored { get; set; }
    [JsonPropertyName("nickname")] public string? Nickname { get; set; }
    [JsonPropertyName("lastActive")] public DateTime LastActive { get; set; }
    [JsonPropertyName("voice")] public string? Voice { get; set; }
}

public class SbAliasesFile
{
    [JsonPropertyName("aliases")] public List<SbAlias> Aliases { get; set; } = new();
}

public class SbAlias
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("audioDevice")] public string? AudioDevice { get; set; }
    [JsonPropertyName("voices")] public List<SbVoice> Voices { get; set; } = new();
}

public class SbVoice
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("volume")] public double Volume { get; set; } = 1.0;
    [JsonPropertyName("pitch")] public double Pitch { get; set; } = 0.0;
    [JsonPropertyName("rate")] public double Rate { get; set; } = 0.0;
}
