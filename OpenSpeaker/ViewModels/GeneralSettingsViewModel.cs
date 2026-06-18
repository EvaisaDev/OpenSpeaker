using System.Collections.ObjectModel;
using OpenSpeaker.Audio;
using OpenSpeaker.Data;
using OpenSpeaker.Localization;
using OpenSpeaker.Models;
namespace OpenSpeaker.ViewModels;

public class GeneralSettingsViewModel : BaseViewModel
{
    private readonly SettingsRepository _settingsRepo;
    private readonly AudioDeviceEnumerator _deviceEnumerator;
    private AppSettings _settings;

    public ObservableCollection<AudioDeviceInfo> OutputDevices { get; } = new();

    public string InstanceId => _settings.InstanceId;
    public string InstanceName { get => _settings.InstanceName; set { _settings.InstanceName = value; OnPropertyChanged(); } }
    public bool Enabled { get => _settings.Enabled; set { _settings.Enabled = value; OnPropertyChanged(); } }
    public string AudioOutputDeviceId { get => _settings.AudioOutputDeviceId; set { _settings.AudioOutputDeviceId = value; OnPropertyChanged(); } }
    public int ApplicationVolume { get => _settings.ApplicationVolume; set { _settings.ApplicationVolume = value; OnPropertyChanged(); } }
    public bool SaveTts { get => _settings.SaveTts; set { _settings.SaveTts = value; OnPropertyChanged(); } }
    public string SaveTtsFolder { get => _settings.SaveTtsFolder; set { _settings.SaveTtsFolder = value; OnPropertyChanged(); } }
    public string DefaultVoiceAlias { get => _settings.DefaultVoiceAlias; set { _settings.DefaultVoiceAlias = value; OnPropertyChanged(); } }
    public string HighlightVoiceAlias { get => _settings.HighlightVoiceAlias; set { _settings.HighlightVoiceAlias = value; OnPropertyChanged(); } }
    public bool UseHighlightVoice { get => _settings.UseHighlightVoice; set { _settings.UseHighlightVoice = value; OnPropertyChanged(); } }
    public bool MinimizeToTray { get => _settings.MinimizeToTray; set { _settings.MinimizeToTray = value; OnPropertyChanged(); } }
    public bool ConfirmationOnClose { get => _settings.ConfirmationOnClose; set { _settings.ConfirmationOnClose = value; OnPropertyChanged(); } }
    public string LogLevel { get => _settings.LogLevel; set { _settings.LogLevel = value; OnPropertyChanged(); } }

    public IEnumerable<string> LogLevels { get; } = new[] { "Debug", "Info", "Warn", "Error" };
    public IEnumerable<string> AvailableLanguages => LocalizationService.AvailableLanguages;

    public string Language
    {
        get => _settings.Language;
        set { _settings.Language = value; OnPropertyChanged(); LocalizationService.Load(value); }
    }

    public bool ShowTooltips
    {
        get => _settings.ShowTooltips;
        set { _settings.ShowTooltips = value; OnPropertyChanged(); UiController.Instance.ShowTooltips = value; }
    }

    public bool DisableAudioOutput
    {
        get => _settings.DisableAudioOutput;
        set { _settings.DisableAudioOutput = value; OnPropertyChanged(); }
    }

    public bool SimultaneousMode
    {
        get => _settings.SimultaneousMode;
        set { _settings.SimultaneousMode = value; OnPropertyChanged(); }
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand BrowseFolderCommand { get; }
    public RelayCommand RefreshDevicesCommand { get; }
    public RelayCommand ResetInstanceIdCommand { get; }

    public GeneralSettingsViewModel(SettingsRepository settingsRepo, AudioDeviceEnumerator deviceEnumerator)
    {
        _settingsRepo = settingsRepo;
        _deviceEnumerator = deviceEnumerator;
        _settings = settingsRepo.GetSettings();
        foreach (var d in deviceEnumerator.GetOutputDevices()) OutputDevices.Add(d);

        if (OutputDevices.Count > 0 && !OutputDevices.Any(d => d.Id == (_settings.AudioOutputDeviceId ?? "")))
        {
            _settings.AudioOutputDeviceId = OutputDevices[0].Id;
            settingsRepo.SaveSettings(_settings);
        }
        _settings.AudioOutputDeviceId ??= string.Empty;

        SaveCommand = new RelayCommand(Save);
        BrowseFolderCommand = new RelayCommand(BrowseFolder);
        RefreshDevicesCommand = new RelayCommand(RefreshDevices);
        ResetInstanceIdCommand = new RelayCommand(ResetInstanceId);
    }

    private void Save()
    {
        _settingsRepo.SaveSettings(_settings);
    }

    private void BrowseFolder()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog();
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            SaveTtsFolder = dialog.SelectedPath;
    }

    private void RefreshDevices()
    {
        var current = AudioOutputDeviceId;
        OutputDevices.Clear();
        foreach (var d in _deviceEnumerator.GetOutputDevices()) OutputDevices.Add(d);
        AudioOutputDeviceId = current;
    }

    private void ResetInstanceId()
    {
        _settings.InstanceId = Guid.NewGuid().ToString();
        OnPropertyChanged(nameof(InstanceId));
        Save();
    }
}
