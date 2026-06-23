using OpenSpeaker.Api;
using OpenSpeaker.Data;
using OpenSpeaker.Models;
namespace OpenSpeaker.ViewModels;

public class UdpServerViewModel : BaseViewModel
{
    private readonly UdpServer _server;
    private readonly SettingsRepository _settingsRepo;
    private AppSettings _settings;

    public bool AutoStart { get => _settings.UdpServer.AutoStart; set { _settings.UdpServer.AutoStart = value; OnPropertyChanged(); Save(); } }
    public bool IsRunning => _server.IsRunning;

    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }

    public UdpServerViewModel(UdpServer server, SettingsRepository settingsRepo)
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
