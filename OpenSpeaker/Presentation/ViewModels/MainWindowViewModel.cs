using System.Windows;
using OpenSpeaker.Core;
using OpenSpeaker.Import;
using OpenSpeaker.Views;
namespace OpenSpeaker.ViewModels;

public class MainWindowViewModel : BaseViewModel, IDisposable
{
    private readonly AppBootstrapper _boot;

    public EventLogViewModel EventLog { get; }
    public ProcessingViewModel Processing { get; }
    public UsersViewModel Users { get; }
    public EventsViewModel Events { get; }
    public CustomCommandsViewModel CustomCommands { get; }
    public BuiltInCommandsViewModel BuiltInCommands { get; }
    public ChannelRewardsViewModel ChannelRewards { get; }
    public QueueStatusViewModel QueueStatus { get; }
    public GeneralSettingsViewModel GeneralSettings { get; }
    public AccountsViewModel Accounts { get; }
    public VoiceGateViewModel VoiceGate { get; }
    public SpeechEnginesViewModel SpeechEngines { get; }
    public IgnoredVoicesViewModel IgnoredVoices { get; }
    public SpeakingOptionsViewModel SpeakingOptions { get; }
    public ReplacementViewModel Replacement { get; }
    public VoiceAliasListViewModel VoiceAliases { get; }
    public WebSocketServerViewModel WebSocketServer { get; }
    public UdpServerViewModel UdpServer { get; }
    public CustomApiViewModel CustomApis { get; }
    public ExtensionsViewModel Extensions { get; }
    public ImportViewModel Import { get; }
    public ProfileViewModel Profile { get; }
    public UpdateViewModel Update { get; }

    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            SetField(ref _isEnabled, value);
            _boot.Orchestrator.SetEnabled(value);
        }
    }

    private string? _updateNotice;
    public string? UpdateNotice { get => _updateNotice; set => SetField(ref _updateNotice, value); }

    public AsyncRelayCommand SilenceTtsCommand { get; }
    public AsyncRelayCommand GenericSpeakCommand { get; }

    public MainWindowViewModel(AppBootstrapper boot, ProfileViewModel profile)
    {
        _boot = boot;
        Profile = profile;

        var settings = boot.SettingsRepo.GetSettings();
        _isEnabled = settings.Enabled;

        EventLog = new EventLogViewModel(boot.Logger);
        Processing = new ProcessingViewModel(boot.Queue);
        Users = new UsersViewModel(boot.UserRepo, boot.AliasRepo);
        Events = new EventsViewModel(boot.EventConfigRepo, boot.SettingsRepo, boot.AliasRepo);
        CustomCommands = new CustomCommandsViewModel(boot.CustomCommandRepo, boot.AliasRepo);
        BuiltInCommands = new BuiltInCommandsViewModel(boot.SettingsRepo);
        ChannelRewards = new ChannelRewardsViewModel(boot.ChannelRewardRepo, boot.TwitchAuth, boot.AliasRepo);
        QueueStatus = new QueueStatusViewModel(boot.Queue);
        GeneralSettings = new GeneralSettingsViewModel(boot.SettingsRepo, boot.DeviceEnumerator, boot.AliasRepo);
        Accounts = new AccountsViewModel(boot.TwitchAuth, boot.SettingsRepo);
        Accounts.SetTwitchService(boot.Twitch);
        VoiceGate = new VoiceGateViewModel(boot.VoiceGate, boot.Database, boot.DeviceEnumerator);
        SpeechEngines = new SpeechEnginesViewModel(boot.Database, boot.EngineRegistry, boot.VoicePool, boot.Extensions, boot.AliasRepo, boot.Logger);
        IgnoredVoices = new IgnoredVoicesViewModel(boot.Database, boot.VoicePool);
        SpeakingOptions = new SpeakingOptionsViewModel(boot.SettingsRepo, boot.EmoteCache, boot.Twitch, boot.AliasRepo);
        Replacement = new ReplacementViewModel(boot.RegexReplacementRepo, boot.SettingsRepo);
        VoiceAliases = new VoiceAliasListViewModel(boot.AliasRepo, boot.EngineRegistry, boot.VoicePool, boot.DeviceEnumerator, boot.UserRepo, () => Users.AllUsers, boot.Logger)
        {
            OnAliasesChanged = () =>
            {
                if (SpeechEngines.SelectedEngine != null)
                    SpeechEngines.RefreshEngineAliases(SpeechEngines.SelectedEngine);
            }
        };
        WebSocketServer = new WebSocketServerViewModel(boot.WsServer, boot.SettingsRepo);
        UdpServer = new UdpServerViewModel(boot.UdpServer, boot.SettingsRepo);
        CustomApis = new CustomApiViewModel(boot.Database, boot.EngineRegistry);
        Extensions = new ExtensionsViewModel(boot.Extensions, boot.EngineRegistry, boot.VoicePool);

        var importer = new SpeakerBotImporter(boot.SettingsRepo, boot.UserRepo, boot.AliasRepo, boot.EventConfigRepo, boot.ChannelRewardRepo, boot.Database, boot.EngineRegistry, boot.Logger);
        Import = new ImportViewModel(importer, boot.Extensions)
        {
            OnComplete = () =>
            {
                boot.SettingsRepo.Invalidate();
                boot.CustomCommandRepo.Invalidate();
                boot.RegexReplacementRepo.Invalidate();
                Users.Refresh();
                VoiceAliases.Refresh();
                SpeechEngines.Refresh();
                Events.Refresh();
                ChannelRewards.Refresh();
                CustomCommands.Refresh();
                BuiltInCommands.Refresh();
                Replacement.Refresh();
                IgnoredVoices.Refresh();
                GeneralSettings.Refresh();
                SpeakingOptions.Refresh();
                WebSocketServer.Refresh();
                UdpServer.Refresh();
            }
        };

        boot.Twitch.ChatMessage += (_, e) =>
        {
            Users.OnChatMessage(e.UserId);
            VoiceAliases.NotifyUserActivity(e.UserId);
        };

        SilenceTtsCommand = new AsyncRelayCommand(SilenceTtsAsync);
        GenericSpeakCommand = new AsyncRelayCommand(GenericSpeakAsync);

        Update = new UpdateViewModel(boot.SettingsRepo);
        _ = Update.InitializeAsync();
    }

    private async Task SilenceTtsAsync()
    {
        _boot.Queue.Stop();
        _boot.Queue.Clear();
    }

    private async Task GenericSpeakAsync()
    {
        var vm = new GenericSpeakerViewModel(_boot.EngineRegistry, _boot.VoicePool, _boot.Logger);
        var window = new GenericSpeakerWindow(vm);
        window.Owner = Application.Current.MainWindow;
        window.Show();
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        VoiceGate.Dispose();
        VoiceAliases.Dispose();
    }
}
