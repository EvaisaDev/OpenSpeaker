using System.Collections.ObjectModel;
using OpenSpeaker.Audio;
using OpenSpeaker.Data;
using OpenSpeaker.Localization;
using OpenSpeaker.Themes;
namespace OpenSpeaker.ViewModels;

public class GeneralSettingsViewModel : SettingsViewModelBase
{
    private readonly AudioDeviceEnumerator _deviceEnumerator;
    private readonly VoiceAliasRepository _aliasRepo;
    private readonly IDialogService _dialogs;

    public ObservableCollection<AudioDeviceInfo> OutputDevices { get; } = new();

    public ObservableCollection<string> AvailableAliases { get; } = new();

    public string InstanceId => Settings.InstanceId;
    public string InstanceName { get => Settings.InstanceName; set => Set(s => s.InstanceName = value); }
    public bool Enabled { get => Settings.Enabled; set => Set(s => s.Enabled = value); }
    public string AudioOutputDeviceId { get => Settings.AudioOutputDeviceId; set => Set(s => s.AudioOutputDeviceId = value); }
    public int ApplicationVolume { get => Settings.ApplicationVolume; set => Set(s => s.ApplicationVolume = value); }
    public bool SaveTts { get => Settings.SaveTts; set => Set(s => s.SaveTts = value); }
    public string SaveTtsFolder { get => Settings.SaveTtsFolder; set => Set(s => s.SaveTtsFolder = value); }
    public string DefaultVoiceAlias
    {
        get => string.IsNullOrEmpty(Settings.DefaultVoiceAlias) ? NoneAlias : Settings.DefaultVoiceAlias;
        set => Set(s => s.DefaultVoiceAlias = value == NoneAlias ? string.Empty : value);
    }
    public string HighlightVoiceAlias
    {
        get => string.IsNullOrEmpty(Settings.HighlightVoiceAlias) ? NoneAlias : Settings.HighlightVoiceAlias;
        set => Set(s => s.HighlightVoiceAlias = value == NoneAlias ? string.Empty : value);
    }
    public bool UseHighlightVoice { get => Settings.UseHighlightVoice; set => Set(s => s.UseHighlightVoice = value); }
    public bool MinimizeToTray { get => Settings.MinimizeToTray; set => Set(s => s.MinimizeToTray = value); }
    public bool ConfirmationOnClose { get => Settings.ConfirmationOnClose; set => Set(s => s.ConfirmationOnClose = value); }
    public string LogLevel { get => Settings.LogLevel; set => Set(s => s.LogLevel = value); }

    public IEnumerable<string> LogLevels { get; } = new[] { "Debug", "Info", "Warn", "Error" };
    public IEnumerable<string> AvailableLanguages => LocalizationService.AvailableLanguages;
    public IEnumerable<string> AvailableThemes => ThemeService.AvailableThemes;

    public string Language
    {
        get => Settings.Language;
        set { Set(s => s.Language = value); LocalizationService.Load(value); }
    }

    public string Theme
    {
        get => Settings.Theme;
        set { Set(s => s.Theme = value); ThemeService.Apply(value); }
    }

    public bool ShowTooltips
    {
        get => Settings.ShowTooltips;
        set { Set(s => s.ShowTooltips = value); UiController.Instance.ShowTooltips = value; }
    }

    public bool DisableAudioOutput
    {
        get => Settings.DisableAudioOutput;
        set => Set(s => s.DisableAudioOutput = value);
    }

    public string QueueMode
    {
        get => Settings.QueueMode;
        set => Set(s => s.QueueMode = value);
    }

    public IEnumerable<string> QueueModes => Models.QueueModes.All;

    public RelayCommand BrowseFolderCommand { get; }
    public RelayCommand RefreshDevicesCommand { get; }
    public RelayCommand ResetInstanceIdCommand { get; }
    public RelayCommand RefreshThemesCommand { get; }
    public RelayCommand OpenThemesFolderCommand { get; }
    public RelayCommand RefreshLanguagesCommand { get; }
    public RelayCommand OpenLanguagesFolderCommand { get; }

    public GeneralSettingsViewModel(SettingsRepository settingsRepo, AudioDeviceEnumerator deviceEnumerator, VoiceAliasRepository aliasRepo, IDialogService? dialogs = null)
        : base(settingsRepo)
    {
        _deviceEnumerator = deviceEnumerator;
        _aliasRepo = aliasRepo;
        _dialogs = dialogs ?? new DialogService();
        RefreshAliases();
        foreach (var d in deviceEnumerator.GetOutputDevices()) OutputDevices.Add(d);

        if (OutputDevices.Count > 0 && !OutputDevices.Any(d => d.Id == (Settings.AudioOutputDeviceId ?? "")))
        {
            Settings.AudioOutputDeviceId = OutputDevices[0].Id;
            SettingsRepo.SaveSettings(Settings);
        }
        Settings.AudioOutputDeviceId ??= string.Empty;

        BrowseFolderCommand = new RelayCommand(BrowseFolder);
        RefreshDevicesCommand = new RelayCommand(RefreshDevices);
        ResetInstanceIdCommand = new RelayCommand(ResetInstanceId);
        RefreshThemesCommand = new RelayCommand(() => OnPropertyChanged(nameof(AvailableThemes)));
        OpenThemesFolderCommand = new RelayCommand(() => OpenFolder(ThemeService.ThemesDirectory));
        RefreshLanguagesCommand = new RelayCommand(() => OnPropertyChanged(nameof(AvailableLanguages)));
        OpenLanguagesFolderCommand = new RelayCommand(() => OpenFolder(LocalizationService.LocalizationDirectory));
    }

    public void RefreshAliases()
    {
        AvailableAliases.Clear();
        AvailableAliases.Add(NoneAlias);
        foreach (var a in _aliasRepo.GetAllSorted())
            AvailableAliases.Add(a.Name);
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

    private void BrowseFolder()
    {
        var path = _dialogs.PickFolder();
        if (path != null) SaveTtsFolder = path;
    }

    private void RefreshDevices()
    {
        var current = AudioOutputDeviceId;
        OutputDevices.Clear();
        foreach (var d in _deviceEnumerator.GetOutputDevices()) OutputDevices.Add(d);
        AudioOutputDeviceId = current;
    }

    private void ResetInstanceId() =>
        Set(s => s.InstanceId = Guid.NewGuid().ToString(), nameof(InstanceId));
}
