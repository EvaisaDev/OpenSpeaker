using System.Windows;
using OpenSpeaker.Core;
using OpenSpeaker.Import;
using OpenSpeaker.Views;
namespace OpenSpeaker.ViewModels;

public class MainWindowViewModel : BaseViewModel
{
    private readonly AppBootstrapper _boot;

    public EventLogViewModel EventLog { get; }
    public ProcessingViewModel Processing { get; }
    public UsersViewModel Users { get; }
    public EventsViewModel Events { get; }
    public CustomCommandsViewModel CustomCommands { get; }
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
    public CustomApiViewModel CustomApis { get; }
    public ImportViewModel Import { get; }

    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            SetField(ref _isEnabled, value);
            ServiceLocator.Instance?.Orchestrator.SetEnabled(value);
        }
    }

    private string? _updateNotice;
    public string? UpdateNotice { get => _updateNotice; set => SetField(ref _updateNotice, value); }

    public AsyncRelayCommand SilenceTtsCommand { get; }
    public AsyncRelayCommand GenericSpeakCommand { get; }
    public RelayCommand SaveAllCommand { get; }

    public MainWindowViewModel(AppBootstrapper boot)
    {
        _boot = boot;

        var settings = boot.SettingsRepo.GetSettings();
        _isEnabled = settings.Enabled;

        EventLog = new EventLogViewModel(boot.Logger);
        Processing = new ProcessingViewModel(boot.Queue);
        Users = new UsersViewModel(boot.UserRepo, boot.AliasRepo);
        Events = new EventsViewModel(boot.EventConfigRepo, boot.SettingsRepo, boot.AliasRepo);
        CustomCommands = new CustomCommandsViewModel(boot.CustomCommandRepo, boot.AliasRepo);
        ChannelRewards = new ChannelRewardsViewModel(boot.ChannelRewardRepo, boot.TwitchAuth, boot.AliasRepo);
        QueueStatus = new QueueStatusViewModel(boot.Queue);
        GeneralSettings = new GeneralSettingsViewModel(boot.SettingsRepo, boot.DeviceEnumerator);
        Accounts = new AccountsViewModel(boot.TwitchAuth, boot.SettingsRepo);
        Accounts.SetTwitchService(boot.Twitch);
        VoiceGate = new VoiceGateViewModel(boot.VoiceGate, boot.Database, boot.DeviceEnumerator);
        SpeechEngines = new SpeechEnginesViewModel(boot.Database, boot.EngineRegistry, boot.VoicePool);
        IgnoredVoices = new IgnoredVoicesViewModel(boot.Database, boot.EngineRegistry);
        SpeakingOptions = new SpeakingOptionsViewModel(boot.SettingsRepo);
        Replacement = new ReplacementViewModel(boot.Database, boot.SettingsRepo);
        VoiceAliases = new VoiceAliasListViewModel(boot.AliasRepo, boot.EngineRegistry, boot.DeviceEnumerator, boot.UserRepo, boot.Logger);
        WebSocketServer = new WebSocketServerViewModel(boot.WsServer, boot.SettingsRepo);
        CustomApis = new CustomApiViewModel(boot.Database, boot.EngineRegistry);

        var importer = new SpeakerBotImporter(boot.SettingsRepo, boot.UserRepo, boot.AliasRepo, boot.EventConfigRepo, boot.ChannelRewardRepo, boot.Database);
        Import = new ImportViewModel(importer);

        boot.Twitch.ChatMessage += (_, e) => Users.OnChatMessage(e.UserId);

        SilenceTtsCommand = new AsyncRelayCommand(SilenceTtsAsync);
        GenericSpeakCommand = new AsyncRelayCommand(GenericSpeakAsync);
        SaveAllCommand = new RelayCommand(SaveAll);
    }

    private async Task SilenceTtsAsync()
    {
        _boot.Queue.Stop();
        _boot.Queue.Clear();
    }

    private async Task GenericSpeakAsync()
    {
        var vm = new GenericSpeakerViewModel(_boot.EngineRegistry, _boot.AliasRepo);
        var window = new GenericSpeakerWindow(vm);
        window.Owner = Application.Current.MainWindow;
        window.Show();
        await Task.CompletedTask;
    }

    private void SaveAll()
    {
        GeneralSettings.SaveCommand.Execute(null);
        SpeakingOptions.SaveCommand.Execute(null);
        WebSocketServer.SaveCommand.Execute(null);
    }
}
