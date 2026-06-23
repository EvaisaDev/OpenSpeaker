using OpenSpeaker.Api;
using OpenSpeaker.Data;
using OpenSpeaker.Models;
namespace OpenSpeaker.ViewModels;

public class WebSocketServerViewModel : SettingsViewModelBase
{
    private readonly WebSocketServer _server;

    public string Address { get => Settings.WebSocketServer.Address; set => Set(s => s.WebSocketServer.Address = value); }
    public int Port { get => Settings.WebSocketServer.Port; set => Set(s => s.WebSocketServer.Port = value); }
    public string Endpoint { get => Settings.WebSocketServer.Endpoint; set => Set(s => s.WebSocketServer.Endpoint = value); }
    public bool AutoStart { get => Settings.WebSocketServer.AutoStart; set => Set(s => s.WebSocketServer.AutoStart = value); }
    public bool IsRunning { get => _server.IsRunning; }

    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }

    public WebSocketServerViewModel(WebSocketServer server, SettingsRepository settingsRepo) : base(settingsRepo)
    {
        _server = server;

        StartCommand = new RelayCommand(() => { _server.Start(); OnPropertyChanged(nameof(IsRunning)); }, () => !_server.IsRunning);
        StopCommand = new RelayCommand(() => { _server.Stop(); OnPropertyChanged(nameof(IsRunning)); }, () => _server.IsRunning);
    }
}
