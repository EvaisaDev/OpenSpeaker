using OpenSpeaker.Api;
using OpenSpeaker.Data;
using OpenSpeaker.Models;
namespace OpenSpeaker.ViewModels;

public class WebSocketServerViewModel : BaseViewModel
{
    private readonly WebSocketServer _server;
    private readonly SettingsRepository _settingsRepo;
    private AppSettings _settings;

    public string Address { get => _settings.WebSocketServer.Address; set { _settings.WebSocketServer.Address = value; OnPropertyChanged(); } }
    public int Port { get => _settings.WebSocketServer.Port; set { _settings.WebSocketServer.Port = value; OnPropertyChanged(); } }
    public string Endpoint { get => _settings.WebSocketServer.Endpoint; set { _settings.WebSocketServer.Endpoint = value; OnPropertyChanged(); } }
    public bool AutoStart { get => _settings.WebSocketServer.AutoStart; set { _settings.WebSocketServer.AutoStart = value; OnPropertyChanged(); } }
    public bool IsRunning { get => _server.IsRunning; }

    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand SaveCommand { get; }

    public WebSocketServerViewModel(WebSocketServer server, SettingsRepository settingsRepo)
    {
        _server = server;
        _settingsRepo = settingsRepo;
        _settings = settingsRepo.GetSettings();

        StartCommand = new RelayCommand(() => { _server.Start(); OnPropertyChanged(nameof(IsRunning)); }, () => !_server.IsRunning);
        StopCommand = new RelayCommand(() => { _server.Stop(); OnPropertyChanged(nameof(IsRunning)); }, () => _server.IsRunning);
        SaveCommand = new RelayCommand(() => _settingsRepo.SaveSettings(_settings));
    }
}
