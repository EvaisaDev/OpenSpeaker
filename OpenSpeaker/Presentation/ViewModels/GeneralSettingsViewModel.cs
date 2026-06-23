using System.Collections.ObjectModel;
using OpenSpeaker.Audio;
using OpenSpeaker.Data;
using OpenSpeaker.Localization;
using OpenSpeaker.Models;
using OpenSpeaker.Themes;
namespace OpenSpeaker.ViewModels;

public class GeneralSettingsViewModel : BaseViewModel
{
    private readonly SettingsRepository _settingsRepo;
    private readonly AudioDeviceEnumerator _deviceEnumerator;
    private AppSettings _settings;

    public ObservableCollection<AudioDeviceInfo> OutputDevices { get; } = new();

    public string InstanceId => _settings.InstanceId;
    public string InstanceName { get => _settings.InstanceName; set { _settings.InstanceName = value; OnPropertyChanged(); Save(); } }
    public bool Enabled { get => _settings.Enabled; set { _settings.Enabled = value; OnPropertyChanged(); Save(); } }
    public string AudioOutputDeviceId { get => _settings.AudioOutputDeviceId; set { _settings.AudioOutputDeviceId = value; OnPropertyChanged(); Save(); } }
    public int ApplicationVolume { get => _settings.ApplicationVolume; set { _settings.ApplicationVolume = value; OnPropertyChanged(); Save(); } }
    public bool SaveTts { get => _settings.SaveTts; set { _settings.SaveTts = value; OnPropertyChanged(); Save(); } }
    public string SaveTtsFolder { get => _settings.SaveTtsFolder; set { _settings.SaveTtsFolder = value; OnPropertyChanged(); Save(); } }
    public string DefaultVoiceAlias { get => _settings.DefaultVoiceAlias; set { _settings.DefaultVoiceAlias = value; OnPropertyChanged(); Save(); } }
    public string HighlightVoiceAlias { get => _settings.HighlightVoiceAlias; set { _settings.HighlightVoiceAlias = value; OnPropertyChanged(); Save(); } }
    public bool UseHighlightVoice { get => _settings.UseHighlightVoice; set { _settings.UseHighlightVoice = value; OnPropertyChanged(); Save(); } }
    public bool MinimizeToTray { get => _settings.MinimizeToTray; set { _settings.MinimizeToTray = value; OnPropertyChanged(); Save(); } }
    public bool ConfirmationOnClose { get => _settings.ConfirmationOnClose; set { _settings.ConfirmationOnClose = value; OnPropertyChanged(); Save(); } }
    public string LogLevel { get => _settings.LogLevel; set { _settings.LogLevel = value; OnPropertyChanged(); Save(); } }

    public IEnumerable<string> LogLevels { get; } = new[] { "Debug", "Info", "Warn", "Error" };
    public IEnumerable<string> AvailableLanguages => LocalizationService.AvailableLanguages;
    public IEnumerable<string> AvailableThemes => ThemeService.AvailableThemes;

    public string Language
    {
        get => _settings.Language;
        set { _settings.Language = value; OnPropertyChanged(); LocalizationService.Load(value); Save(); }
    }

    public string Theme
    {
        get => _settings.Theme;
        set { _settings.Theme = value; OnPropertyChanged(); ThemeService.Apply(value); Save(); }
    }

    public bool ShowTooltips
    {
        get => _settings.ShowTooltips;
        set { _settings.ShowTooltips = value; OnPropertyChanged(); UiController.Instance.ShowTooltips = value; Save(); }
    }

    public bool DisableAudioOutput
    {
        get => _settings.DisableAudioOutput;
        set { _settings.DisableAudioOutput = value; OnPropertyChanged(); Save(); }
    }

    public string QueueMode
    {
        get => _settings.QueueMode;
        set { _settings.QueueMode = value; OnPropertyChanged(); Save(); }
    }

    public IEnumerable<string> QueueModes => Models.QueueModes.All;

    public RelayCommand BrowseFolderCommand { get; }
    public RelayCommand RefreshDevicesCommand { get; }
    public RelayCommand ResetInstanceIdCommand { get; }
    public RelayCommand RefreshThemesCommand { get; }
    public RelayCommand OpenThemesFolderCommand { get; }
    public RelayCommand RefreshLanguagesCommand { get; }
    public RelayCommand OpenLanguagesFolderCommand { get; }

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

        BrowseFolderCommand = new RelayCommand(BrowseFolder);
        RefreshDevicesCommand = new RelayCommand(RefreshDevices);
        ResetInstanceIdCommand = new RelayCommand(ResetInstanceId);
        RefreshThemesCommand = new RelayCommand(() => OnPropertyChanged(nameof(AvailableThemes)));
        OpenThemesFolderCommand = new RelayCommand(() => OpenFolder(ThemeService.ThemesDirectory));
        RefreshLanguagesCommand = new RelayCommand(() => OnPropertyChanged(nameof(AvailableLanguages)));
        OpenLanguagesFolderCommand = new RelayCommand(() => OpenFolder(LocalizationService.LocalizationDirectory));
    }

    private static void OpenFolder(string path)
    {
        try
        {
            System.IO.Directory.CreateDirectory(path);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch { }
    }

    public void Refresh()
    {
        _settings = _settingsRepo.GetSettings();
        OnPropertyChanged(string.Empty);
    }

    private void Save() => _settingsRepo.SaveSettings(_settings);

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
