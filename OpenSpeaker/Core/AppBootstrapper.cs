using OpenSpeaker.Api;
using OpenSpeaker.Audio;
using OpenSpeaker.Chat;
using OpenSpeaker.Data;
using OpenSpeaker.Events;
using OpenSpeaker.Infrastructure.Logging;
using OpenSpeaker.Models;
using OpenSpeaker.Queue;
using OpenSpeaker.Services;
using OpenSpeaker.Text;
using OpenSpeaker.TTS;
using OpenSpeaker.Twitch;
using OpenSpeaker.Users;
using System.IO;
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
    public ChannelRewardRepository ChannelRewardRepo { get; }
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
    public UdpServer UdpServer { get; }
    public VoiceGateService VoiceGate { get; }
    public EmoteStripper EmoteStripper { get; }
    public VoicePool VoicePool { get; }

    private readonly TtsQueueService _queueService;
    private readonly DatabaseMigration _migration;

    public AppBootstrapper()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var logsDir = Path.Combine(appDir, "logs");

        Database = new DatabaseContext(Path.Combine(appDir, Constants.DatabaseFileName));

        _migration = new DatabaseMigration(Database);
        _migration.Run();

        SettingsRepo = new SettingsRepository(Database);
        var logLevel = SettingsRepo.GetSettings().LogLevel;
        Logger = new AppLogger(logsDir, logLevel);
        UserRepo = new UserRepository(Database);
        AliasRepo = new VoiceAliasRepository(Database);
        EventConfigRepo = new EventConfigRepository(Database);
        CustomCommandRepo = new CustomCommandRepository(Database);
        ChannelRewardRepo = new ChannelRewardRepository(Database);
        DeviceEnumerator = new AudioDeviceEnumerator(Logger);

        EngineRegistry = new TtsEngineRegistry(Database);
        var audioPlayer = new NAudioPlayer();
        var wavSaver = new WavFileSaver();

        PermissionChecker = new PermissionChecker();
        UserService = new UserService(UserRepo, AliasRepo);

        _queueService = new TtsQueueService(EngineRegistry, audioPlayer, () => new NAudioPlayer(), wavSaver, SettingsRepo, AliasRepo, UserService);
        Queue = _queueService;

        EmoteStripper = new EmoteStripper();
        var prefixChecker = new PrefixChecker();
        var regexReplacer = new RegexReplacer();
        var substitutor = new VariableSubstitutor();
        var sanitizer = new MessageSanitizer(EmoteStripper, prefixChecker, regexReplacer, SettingsRepo, Database);

        TwitchAuth = new TwitchAuthService(Database);
        var emoteCache = new EmoteCacheService(TwitchAuth, EmoteStripper, Logger);
        EmoteCache = emoteCache;
        var twitchService = new TwitchEventSubService(TwitchAuth, emoteCache, Logger);
        Twitch = twitchService;

        var messagePicker = new WeightedMessagePicker();
        var variableBuilder = new VariableBuilder();
        var eventProcessor = new EventProcessor(EventConfigRepo, messagePicker, substitutor, Queue, SettingsRepo);
        var _ = new EventDispatcher(Twitch, eventProcessor, variableBuilder);

        VoicePool = new VoicePool(EngineRegistry);
        var voicePool = VoicePool;

        var builtIn = new BuiltInCommandHandler(SettingsRepo, Queue, UserService, UserRepo, EngineRegistry, EventConfigRepo, CustomCommandRepo, Twitch, voicePool);
        var custom = new CustomCommandHandler(Database, PermissionChecker, Queue);
        var sayEverything = new SayEverythingHandler(SettingsRepo, UserService, PermissionChecker, sanitizer, Queue, voicePool, Twitch);

        var orchestrator = new TtsOrchestrator(Queue, SettingsRepo, sanitizer);
        Orchestrator = orchestrator;

        var voiceGateMonitor = new VoiceGateMonitor();
        VoiceGate = new VoiceGateService(voiceGateMonitor, Queue, Database);

        var chatService = new ChatService(Twitch, builtIn, custom, sayEverything, UserService, SettingsRepo, Queue);

        var wsRouter = new WebSocketCommandRouter(Orchestrator, SettingsRepo);
        WsServer = new WebSocketServer(wsRouter, SettingsRepo, Logger);

        var udpRouter = new UdpCommandRouter(Orchestrator, Queue, UserService, SettingsRepo, VoiceGate, EventConfigRepo);
        UdpServer = new UdpServer(udpRouter, Logger);
    }

    public async Task StartAsync()
    {
        var settings = SettingsRepo.GetSettings();

        if (settings.WebSocketServer.AutoStart)
        {
            WsServer.Start();
            Logger.Info("WEBSOCKET :: Websocket Server Started");
        }

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
        UdpServer.Stop();
        VoiceGate.Deactivate();
    }

    public void Dispose()
    {
        _queueService.Dispose();
        EngineRegistry.Dispose();
        VoiceGate.Dispose();
        Database.Dispose();
    }
}
