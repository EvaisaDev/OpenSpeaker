using OpenSpeaker.Api;
using OpenSpeaker.Data;
using OpenSpeaker.Models;
namespace OpenSpeaker.ViewModels;

public class UdpServerViewModel : SettingsViewModelBase
{
    private readonly UdpServer _server;

    public bool AutoStart { get => Settings.UdpServer.AutoStart; set => Set(s => s.UdpServer.AutoStart = value); }
    public bool IsRunning => _server.IsRunning;

    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }

    public UdpServerViewModel(UdpServer server, SettingsRepository settingsRepo) : base(settingsRepo)
    {
        _server = server;

        StartCommand = new RelayCommand(() => { _server.Start(); OnPropertyChanged(nameof(IsRunning)); }, () => !_server.IsRunning);
        StopCommand = new RelayCommand(() => { _server.Stop(); OnPropertyChanged(nameof(IsRunning)); }, () => _server.IsRunning);
    }
}
