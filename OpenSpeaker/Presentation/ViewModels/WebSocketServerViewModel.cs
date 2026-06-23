using OpenSpeaker.Api;
using OpenSpeaker.Data;
using OpenSpeaker.Models;
namespace OpenSpeaker.ViewModels;

public class WebSocketServerViewModel : BaseViewModel
{
    private readonly WebSocketServer _server;
    private readonly SettingsRepository _settingsRepo;
    private AppSettings _settings;

    public string Address { get => _settings.WebSocketServer.Address; set { _settings.WebSocketServer.Address = value; OnPropertyChanged(); Save(); } }
    public int Port { get => _settings.WebSocketServer.Port; set { _settings.WebSocketServer.Port = value; OnPropertyChanged(); Save(); } }
    public string Endpoint { get => _settings.WebSocketServer.Endpoint; set { _settings.WebSocketServer.Endpoint = value; OnPropertyChanged(); Save(); } }
    public bool AutoStart { get => _settings.WebSocketServer.AutoStart; set { _settings.WebSocketServer.AutoStart = value; OnPropertyChanged(); Save(); } }
    public bool IsRunning { get => _server.IsRunning; }

    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }

    public WebSocketServerViewModel(WebSocketServer server, SettingsRepository settingsRepo)
    {
        _server = server;
        _settingsRepo = settingsRepo;
        _settings = settingsRepo.GetSettings();

        StartCommand = new RelayCommand(() => { _server.Start(); OnPropertyChanged(nameof(IsRunning)); }, () => !_server.IsRunning);
        StopCommand = new RelayCommand(() => { _server.Stop(); OnPropertyChanged(nameof(IsRunning)); }, () => _server.IsRunning);
    }

    public void Refresh()
    {
        _settings = _settingsRepo.GetSettings();
        OnPropertyChanged(string.Empty);
    }

    private void Save() => _settingsRepo.SaveSettings(_settings);
}
