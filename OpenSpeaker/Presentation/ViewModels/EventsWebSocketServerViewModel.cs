using OpenSpeaker.Api;
using OpenSpeaker.Data;
namespace OpenSpeaker.ViewModels;

public class EventsWebSocketServerViewModel : SettingsViewModelBase
{
    private readonly EventsWebSocketServer _server;

    public string Address { get => Settings.EventsWebSocketServer.Address; set => Set(s => s.EventsWebSocketServer.Address = value); }
    public int Port { get => Settings.EventsWebSocketServer.Port; set => Set(s => s.EventsWebSocketServer.Port = value); }
    public bool AutoStart { get => Settings.EventsWebSocketServer.AutoStart; set => Set(s => s.EventsWebSocketServer.AutoStart = value); }
    public bool IsRunning { get => _server.IsRunning; }

    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }

    public EventsWebSocketServerViewModel(EventsWebSocketServer server, SettingsRepository settingsRepo) : base(settingsRepo)
    {
        _server = server;

        StartCommand = new RelayCommand(() => { _server.Start(); OnPropertyChanged(nameof(IsRunning)); }, () => !_server.IsRunning);
        StopCommand = new RelayCommand(() => { _server.Stop(); OnPropertyChanged(nameof(IsRunning)); }, () => _server.IsRunning);
    }
}
