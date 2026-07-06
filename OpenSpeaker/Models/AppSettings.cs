using LiteDB;
namespace OpenSpeaker.Models;
public class AppSettings
{
    [BsonId]
    public int Id { get; set; } = 1;

    public string InstanceId { get; set; } = Guid.NewGuid().ToString();
    public string InstanceName { get; set; } = "OpenSpeaker";
    public bool Enabled { get; set; } = true;
    public string LogLevel { get; set; } = "Info";

    public string AudioOutputDeviceId { get; set; } = string.Empty;
    public int ApplicationVolume { get; set; } = 100;
    public bool SaveTts { get; set; } = false;
    public string SaveTtsFolder { get; set; } = string.Empty;

    public string DefaultVoiceAlias { get; set; } = "Default";
    public string HighlightVoiceAlias { get; set; } = string.Empty;
    public bool UseHighlightVoice { get; set; } = false;

    public string Mode { get; set; } = TtsModes.Everything;
    public bool SayUsername { get; set; } = true;
    public string SayUsernamePrefix { get; set; } = "said";
    public bool StickyRandomVoice { get; set; } = false;
    public bool OnlySayUsernameIfDifferent { get; set; } = false;
    public bool ReplaceNameWithNickname { get; set; } = true;
    public bool StopOnMessageDeleted { get; set; } = true;
    public bool SkipOnMessageDeleted { get; set; } = false;
    public bool StopOnUserTimedOut { get; set; } = true;
    public bool SkipOnUserTimedOut { get; set; } = false;
    public bool StopOnUserBanned { get; set; } = true;
    public bool SkipOnUserBanned { get; set; } = false;
    public bool SilenceCommandOutput { get; set; } = false;

    public bool AllowModerators { get; set; } = true;
    public bool AllowSubscribers { get; set; } = true;
    public bool AllowVIPs { get; set; } = true;
    public bool AllowRegulars { get; set; } = true;
    public bool AllowEveryone { get; set; } = true;

    public bool StripTwitchEmotes { get; set; } = true;
    public bool StripBttvEmotes { get; set; } = true;
    public bool StripFfzEmotes { get; set; } = true;
    public bool StripSevenTvEmotes { get; set; } = true;
    public bool StripCheermotes { get; set; } = true;
    public bool StripTwemoji { get; set; } = false;
    public bool AllowFirstEmote { get; set; } = false;

    public List<string> TtsCommands { get; set; } = new() { "!tts", "!say" };
    public string BuiltInCommandName { get; set; } = "!tts";
    public List<BuiltInCommandConfig> BuiltInCommands { get; set; } = new();
    public List<string> IgnoredPrefixes { get; set; } = new() { "!", "/" };

    public bool EventsEnabled { get; set; } = true;
    public string GlobalEventVoiceAlias { get; set; } = string.Empty;

    public WebSocketServerSettings WebSocketServer { get; set; } = new();
    public UdpServerSettings UdpServer { get; set; } = new();
    public double WindowLeft { get; set; } = 100;
    public double WindowTop { get; set; } = 100;
    public double WindowWidth { get; set; } = 900;
    public double WindowHeight { get; set; } = 700;

    public int CooldownSeconds { get; set; } = 0;
    public int MaxWords { get; set; } = 0;
    public int MaxChars { get; set; } = 0;
    public bool WordLimitSymbolsAsSpaces { get; set; } = false;
    public string WordLimitSymbols { get; set; } = "-_";
    public string NotAllowedText { get; set; } = "You're not cool enough to have a voice";
    public string UrlFilterMode { get; set; } = "Disabled";

    public bool MinimizeToTray { get; set; } = false;
    public bool ConfirmationOnClose { get; set; } = false;

    public string Language { get; set; } = "English";
    public string Theme { get; set; } = "Dark";
    public bool ShowTooltips { get; set; } = true;
    public bool DisableAudioOutput { get; set; } = false;
    public bool SimultaneousMode { get; set; } = false;
    public string QueueMode { get; set; } = QueueModes.Sequential;

    public List<string> AllowedEmotes { get; set; } = new();

    public string LastSeenVersion { get; set; } = string.Empty;
}
