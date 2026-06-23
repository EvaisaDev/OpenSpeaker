using OpenSpeaker.Services;
namespace OpenSpeaker.ViewModels;

public class UpdateViewModel : BaseViewModel
{
    private static bool _promptShown;

    private string _versionText = "";
    public string VersionText { get => _versionText; set => SetField(ref _versionText, value); }

    private bool _isUpdateAvailable;
    public bool IsUpdateAvailable { get => _isUpdateAvailable; set => SetField(ref _isUpdateAvailable, value); }

    private string _updateButtonText = "";
    public string UpdateButtonText { get => _updateButtonText; set => SetField(ref _updateButtonText, value); }

    private UpdateService.UpdateInfo? _info;
    private readonly IDialogService _dialogs;

    public AsyncRelayCommand UpdateCommand { get; }

    public UpdateViewModel(IDialogService? dialogs = null)
    {
        _dialogs = dialogs ?? new DialogService();
        VersionText = "v" + UpdateService.CurrentVersion;
        UpdateCommand = new AsyncRelayCommand(ApplyAsync, () => IsUpdateAvailable);
    }

    public async Task InitializeAsync()
    {
        var info = await UpdateService.CheckAsync();
        _info = info;
        if (!info.IsAvailable) return;

        IsUpdateAvailable = true;
        UpdateButtonText = $"Update to v{info.LatestVersion}";

        if (_promptShown) return;
        _promptShown = true;

        if (_dialogs.Confirm(
                $"A new version of OpenSpeaker is available.\n\nCurrent: v{info.CurrentVersion}\nLatest: v{info.LatestVersion}\n\nUpdate now? The app will download the new version and restart.",
                "Update Available"))
            await ApplyAsync();
    }

    private async Task ApplyAsync()
    {
        if (_info is not { IsAvailable: true }) return;
        try
        {
            await UpdateService.ApplyAsync(_info);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"Update failed:\n\n{ex.Message}", "Update");
        }
    }
}
