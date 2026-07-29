using Newtonsoft.Json;
using OpenSpeaker.Api;
using OpenSpeaker.Audio;
using OpenSpeaker.Chat;
using OpenSpeaker.Data;
using OpenSpeaker.Events;
using OpenSpeaker.Extensions;
using OpenSpeaker.Infrastructure.Logging;
using OpenSpeaker.Input;
using OpenSpeaker.Models;
using OpenSpeaker.Queue;
using OpenSpeaker.Services;
using OpenSpeaker.Text;
using OpenSpeaker.TTS;
using OpenSpeaker.Twitch;
using OpenSpeaker.Users;
using System.IO;
using System.Linq;
namespace OpenSpeaker.Core;

public class AppBootstrapper : IDisposable
{
    public IAppLogger Logger { get; }
    public DatabaseContext Database { get; }
    public SettingsRepository SettingsRepo { get; }
    public UserRepository UserRepo { get; }
    public VoiceAliasRepository AliasRepo { get; }
    public EventConfigRepository EventConfigRepo { get; }
    public CustomCommandRepository CustomCommandRepo { get; }
    public RegexReplacementRepository RegexReplacementRepo { get; }
    public ChannelRewardRepository ChannelRewardRepo { get; }
    public KeybindService Keybinds { get; }
    public ExtensionManager Extensions { get; }
    public TtsEngineRegistry EngineRegistry { get; }
    public AudioDeviceEnumerator DeviceEnumerator { get; }
    public ITtsQueue Queue { get; }
    public ITtsOrchestrator Orchestrator { get; }
    public ITwitchService Twitch { get; }
    public TwitchAuthService TwitchAuth { get; }
    public EmoteCacheService EmoteCache { get; }
    public UserService UserService { get; }
    public PermissionChecker PermissionChecker { get; }
    public WebSocketServer WsServer { get; }
    public EventsWebSocketServer EventsWsServer { get; }
    public UdpServer UdpServer { get; }
    public VoiceGateService VoiceGate { get; }
    public EmoteStripper EmoteStripper { get; }
    public VoicePool VoicePool { get; }

    private readonly TtsQueueService _queueService;
    private readonly DatabaseMigration _migration;
    private readonly NAudioPlayer _soundClipPlayer;

    public AppBootstrapper(string? dbPath = null)
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var logsDir = Path.Combine(appDir, "logs");

        Database = new DatabaseContext(dbPath ?? Path.Combine(appDir, Constants.DatabaseFileName));

        _migration = new DatabaseMigration(Database);
        _migration.Run();

        SettingsRepo = new SettingsRepository(Database);
        var logLevel = SettingsRepo.GetSettings().LogLevel;
        Logger = new AppLogger(logsDir, logLevel);
        UserRepo = new UserRepository(Database);
        AliasRepo = new VoiceAliasRepository(Database);
        AliasRepo.EnsureDefaultAlias(OpenSpeaker.TTS.Engines.Sapi5Engine.GetDefaultVoiceName());
        EventConfigRepo = new EventConfigRepository(Database);
        CustomCommandRepo = new CustomCommandRepository(Database);
        RegexReplacementRepo = new RegexReplacementRepository(Database);
        ChannelRewardRepo = new ChannelRewardRepository(Database);
        DeviceEnumerator = new AudioDeviceEnumerator(Logger);

        Keybinds = new KeybindService(Logger);
        Extensions = new ExtensionManager(Database, Keybinds, Logger);
        EngineRegistry = new TtsEngineRegistry(Database, Extensions, Logger);
        var audioPlayer = new NAudioPlayer();
        _soundClipPlayer = new NAudioPlayer();
        var wavSaver = new WavFileSaver();

        PermissionChecker = new PermissionChecker();
        UserService = new UserService(UserRepo, AliasRepo);

        var voiceResolver = new VoiceResolver(EngineRegistry, AliasRepo);
        var playbackCoordinator = new PlaybackCoordinator(audioPlayer);
        var synthesizer = new TtsSynthesizer(voiceResolver, wavSaver, SettingsRepo, UserService, Logger);
        _queueService = new TtsQueueService(synthesizer, playbackCoordinator, () => new NAudioPlayer(), SettingsRepo, Extensions, Logger);
        Queue = _queueService;

        EmoteStripper = new EmoteStripper();
        var prefixChecker = new PrefixChecker();
        var regexReplacer = new RegexReplacer();
        var substitutor = new VariableSubstitutor();
        var sanitizer = new MessageSanitizer(EmoteStripper, prefixChecker, regexReplacer, SettingsRepo, RegexReplacementRepo, Logger);

        TwitchAuth = new TwitchAuthService(Database);
        var emoteCache = new EmoteCacheService(TwitchAuth, EmoteStripper, Logger);
        EmoteCache = emoteCache;
        var twitchService = new TwitchEventSubService(TwitchAuth, emoteCache, Logger);
        Twitch = twitchService;
        Extensions.SetChatSender(msg => Twitch.SendChatMessageAsync(msg));
        Extensions.SetTimeoutSender((userId, seconds, reason) => Twitch.TimeoutUserAsync(userId, seconds, reason));
        Extensions.SetSoundPlayer(async path =>
        {
            var audio = await AudioFileLoader.LoadAsync(path);
            if (audio.IsEmpty) return;
            var soundSettings = SettingsRepo.GetSettings();
            await _soundClipPlayer.PlayAsync(audio, soundSettings.AudioOutputDeviceId, soundSettings.ApplicationVolume);
        });
        Extensions.SetGetPlayingQueueItems(() => Queue.GetPlayingItems().Select(ToQueueEntryInfo).ToList());
        Extensions.SetGetQueuedItems(() => Queue.GetQueuedItems().Select(ToQueueEntryInfo).ToList());
        Extensions.SetStopQueueItem(id => Queue.StopId(id));
        Extensions.SetRemoveQueueItem(id => Queue.RemoveId(id));

        var messagePicker = new WeightedMessagePicker();
        var variableBuilder = new VariableBuilder();
        var eventProcessor = new EventProcessor(EventConfigRepo, messagePicker, substitutor, Queue, SettingsRepo);
        var _ = new EventDispatcher(Twitch, eventProcessor, variableBuilder, Logger);

        VoicePool = new VoicePool(EngineRegistry, Database, Logger);
        var voicePool = VoicePool;

        var builtIn = new BuiltInCommandHandler(SettingsRepo, Queue, UserService, UserRepo, EngineRegistry, CustomCommandRepo, Twitch, voicePool);
        var custom = new CustomCommandHandler(CustomCommandRepo, PermissionChecker, Queue);
        var sayEverything = new SayEverythingHandler(SettingsRepo, UserService, PermissionChecker, sanitizer, Queue, voicePool, Twitch, Extensions, Logger);

        var orchestrator = new TtsOrchestrator(Queue, SettingsRepo, sanitizer);
        Orchestrator = orchestrator;

        var voiceGateMonitor = new VoiceGateMonitor();
        VoiceGate = new VoiceGateService(voiceGateMonitor, Queue, Database);

        var chatService = new ChatService(Twitch, builtIn, custom, sayEverything, UserService, SettingsRepo, Queue, Extensions, Logger);

        var wsRouter = new WebSocketCommandRouter(Orchestrator, Queue, SettingsRepo, Database, VoiceGate, Extensions);
        WsServer = new WebSocketServer(wsRouter, SettingsRepo, Logger);
        wsRouter.Broadcast = WsServer.Broadcast;

        var eventsRouter = new WebSocketCommandRouter(Orchestrator, Queue, SettingsRepo, Database, VoiceGate, Extensions, "OpenSpeaker", UpdateService.CurrentVersion);
        EventsWsServer = new EventsWebSocketServer(eventsRouter, SettingsRepo, Logger);
        eventsRouter.Broadcast = EventsWsServer.Broadcast;
        Extensions.SetWsBroadcaster(json =>
        {
            WsServer.Broadcast(json);
            EventsWsServer.Broadcast(json);
        });
        Queue.ItemPlaying += (_, e) => BroadcastUtteranceEvent("UtteranceStarted", e.Item);
        Queue.ItemCompleted += (_, e) => BroadcastUtteranceEvent("UtteranceFinished", e.Item);

        Queue.ItemQueued += (_, e) => BroadcastTtsLifecycleEvent("TextQueued", e.Item, null, TimeSpan.Zero);
        Queue.ItemSynthesized += (_, e) => BroadcastTtsLifecycleEvent("EngineProcessed", e.Item, e.OutputFilePath, e.Duration);
        Queue.ItemPlaying += (_, e) => BroadcastTtsLifecycleEvent("Playing", e.Item, e.OutputFilePath, e.Duration);
        Queue.ItemCompleted += (_, e) => BroadcastTtsLifecycleEvent("Finished", e.Item, e.OutputFilePath, e.Duration);

        var udpRouter = new UdpCommandRouter(Orchestrator, Queue, UserService, SettingsRepo, VoiceGate, EventConfigRepo);
        UdpServer = new UdpServer(udpRouter, Logger);
    }

    private static QueueEntryInfo ToQueueEntryInfo(TtsQueueItem item) =>
        new(item.SpeechId, item.Text, item.Username, item.UserId);

    private void BroadcastUtteranceEvent(string type, TtsQueueItem item)
    {
        var payload = new
        {
            timeStamp = DateTime.Now.ToString("O"),
            @event = new { source = "Speaker", type },
            data = new { text = item.Text, username = item.Username, alias = item.VoiceAliasName }
        };
        EventsWsServer.Broadcast(JsonConvert.SerializeObject(payload));
    }

    private void BroadcastTtsLifecycleEvent(string type, TtsQueueItem item, string? filename, TimeSpan duration)
    {
        var alias = AliasRepo.GetByName(item.VoiceAliasName);
        var data = new Dictionary<string, object?>();
        if (!string.IsNullOrEmpty(filename) && (type == "EngineProcessed" || type == "Finished"))
            data["filename"] = filename;
        data["id"] = item.SpeechId;
        data["timestamp"] = item.QueuedAt.ToString("O");
        data["text"] = item.Text;
        data["engineName"] = alias?.EngineId ?? string.Empty;
        data["voiceName"] = alias?.VoiceId ?? string.Empty;
        data["pitch"] = 1.0;
        data["volume"] = (alias?.Volume ?? 100) / 100.0;
        data["rate"] = 0.0;
        data["duration"] = duration.TotalMilliseconds;

        var payload = new
        {
            timeStamp = DateTime.Now.ToString("O"),
            @event = new { source = "TextToSpeech", type },
            data
        };
        WsServer.Broadcast(JsonConvert.SerializeObject(payload));
    }

    public async Task StartAsync()
    {
        var settings = SettingsRepo.GetSettings();

        Extensions.StartUpdateLoop();

        if (settings.WebSocketServer.AutoStart)
        {
            WsServer.Start();
            Logger.Info("WEBSOCKET :: Websocket Server Started");
        }

        if (settings.EventsWebSocketServer.AutoStart)
        {
            EventsWsServer.Start();
            Logger.Info("EVENTS WEBSOCKET :: Events WebSocket Server Started");
        }

        if (settings.UdpServer.AutoStart)
            UdpServer.Start();

        if (TwitchAuth.HasValidAccount())
        {
            try { await Twitch.ConnectAsync(); }
            catch (Exception ex) { Logger.Error("Failed to connect to Twitch", ex); }
        }
    }

    public async Task StopAsync()
    {
        await Twitch.DisconnectAsync();
        WsServer.Stop();
        EventsWsServer.Stop();
        UdpServer.Stop();
        VoiceGate.Deactivate();
    }

    public void Dispose()
    {
        _queueService.Dispose();
        _soundClipPlayer.Dispose();
        EngineRegistry.Dispose();
        Extensions.Dispose();
        Keybinds.Dispose();
        VoiceGate.Dispose();
        EventsWsServer.Dispose();
        Database.Dispose();
    }
}
