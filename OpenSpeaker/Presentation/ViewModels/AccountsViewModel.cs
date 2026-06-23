using OpenSpeaker.Data;
using OpenSpeaker.Models;
using OpenSpeaker.Twitch;
namespace OpenSpeaker.ViewModels;

public class AccountsViewModel : BaseViewModel
{
    private readonly TwitchAuthService _twitchAuth;
    private readonly SettingsRepository _settingsRepo;
    private ITwitchService? _twitchService;

    public bool IsTwitchConnected => _twitchAuth.HasValidAccount();
    public string TwitchLogin => _twitchAuth.GetDisplayName() ?? _twitchAuth.GetLogin() ?? string.Empty;
    public string TwitchBroadcasterType => CapFirst(_twitchAuth.GetBroadcasterType() ?? string.Empty);
    public string? TwitchAvatarUrl => _twitchAuth.GetProfileImageUrl() is { Length: > 0 } s ? s : null;
    public string ChatClientStatus => _twitchService?.IsChatConnected == true ? "Disconnect" : "Connect";
    public string EventSubStatus => _twitchService?.IsConnected == true ? "Disconnect" : "Connect";

    public AsyncRelayCommand TwitchConnectCommand { get; }
    public RelayCommand TwitchLogoutCommand { get; }
    public RelayCommand TwitchForgetCommand { get; }
    public AsyncRelayCommand ChatClientToggleCommand { get; }
    public AsyncRelayCommand EventSubToggleCommand { get; }

    public AccountsViewModel(TwitchAuthService twitchAuth, SettingsRepository settingsRepo)
    {
        _twitchAuth = twitchAuth;
        _settingsRepo = settingsRepo;

        TwitchConnectCommand = new AsyncRelayCommand(ConnectTwitchAsync, () => !IsTwitchConnected);
        TwitchLogoutCommand = new RelayCommand(LogoutTwitch, () => IsTwitchConnected);
        TwitchForgetCommand = new RelayCommand(ForgetTwitch, () => IsTwitchConnected);
        ChatClientToggleCommand = new AsyncRelayCommand(ToggleChatClientAsync);
        EventSubToggleCommand = new AsyncRelayCommand(ToggleEventSubAsync);
    }

    public void SetTwitchService(ITwitchService service)
    {
        _twitchService = service;
    }

    private async Task ConnectTwitchAsync()
    {
        var window = new Views.TwitchAuthWindow(_twitchAuth);
        window.ShowDialog();
        NotifyTwitchChanged();
        if (IsTwitchConnected && _twitchService != null && !_twitchService.IsChatConnected)
        {
            await _twitchService.ConnectAsync();
            OnPropertyChanged(nameof(ChatClientStatus));
            OnPropertyChanged(nameof(EventSubStatus));
        }
    }

    private void LogoutTwitch()
    {
        _twitchAuth.ClearAccount();
        NotifyTwitchChanged();
    }

    private void ForgetTwitch()
    {
        _twitchAuth.ClearAccount();
        NotifyTwitchChanged();
    }

    private async Task ToggleChatClientAsync()
    {
        if (_twitchService == null) return;
        if (_twitchService.IsChatConnected)
            await _twitchService.DisconnectAsync();
        else
            await _twitchService.ConnectAsync();
        OnPropertyChanged(nameof(ChatClientStatus));
        OnPropertyChanged(nameof(EventSubStatus));
    }

    private async Task ToggleEventSubAsync()
    {
        if (_twitchService == null) return;
        if (_twitchService.IsConnected)
            await _twitchService.DisconnectAsync();
        else
            await _twitchService.ConnectAsync();
        OnPropertyChanged(nameof(EventSubStatus));
        OnPropertyChanged(nameof(ChatClientStatus));
    }

    private void NotifyTwitchChanged()
    {
        OnPropertyChanged(nameof(IsTwitchConnected));
        OnPropertyChanged(nameof(TwitchLogin));
        OnPropertyChanged(nameof(TwitchBroadcasterType));
        OnPropertyChanged(nameof(TwitchAvatarUrl));
    }

    private static string CapFirst(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];
}
