using System.Collections.ObjectModel;
using System.IO;
using OpenSpeaker.Extensions;
using OpenSpeaker.Services;
using OpenSpeaker.TTS;
namespace OpenSpeaker.ViewModels;

public class ExtensionInfoCard
{
    public string Header { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
}

public class SettingFieldViewModel : BaseViewModel
{
    private string _value = string.Empty;

    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public IReadOnlyList<string> Options { get; init; } = Array.Empty<string>();

    private bool _capturing;
    private string _captureText = string.Empty;

    public string Value
    {
        get => _value;
        set { if (SetField(ref _value, value)) { OnPropertyChanged(nameof(BoolValue)); OnPropertyChanged(nameof(KeybindDisplay)); } }
    }

    public bool BoolValue
    {
        get => _value == "true";
        set { _value = value ? "true" : "false"; OnPropertyChanged(nameof(Value)); OnPropertyChanged(nameof(BoolValue)); }
    }

    public bool IsCapturing
    {
        get => _capturing;
        set { if (SetField(ref _capturing, value)) OnPropertyChanged(nameof(KeybindDisplay)); }
    }

    public string CaptureText
    {
        get => _captureText;
        set { if (SetField(ref _captureText, value)) OnPropertyChanged(nameof(KeybindDisplay)); }
    }

    public string KeybindDisplay =>
        IsCapturing
            ? string.IsNullOrEmpty(_captureText) ? "Press keys..." : _captureText + "..."
            : string.IsNullOrEmpty(_value) ? "(none)" : _value;

    public bool IsTextLike => Type is "text" or "number";
    public bool IsCheckbox => Type == "checkbox";
    public bool IsDropdown => Type == "dropdown";
    public bool IsKeybind => Type == "keybind";
}

public class ExtensionItem : BaseViewModel
{
    public string EngineId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string FolderPath { get; init; } = string.Empty;
    public IReadOnlyList<ExtensionInfoCard> InfoCards { get; init; } = Array.Empty<ExtensionInfoCard>();

    private string _status = "Loaded";
    public string Status { get => _status; set => SetField(ref _status, value); }
}

public class ExtensionsViewModel : BaseViewModel
{
    private readonly ExtensionManager _extensions;
    private readonly TtsEngineRegistry _registry;
    private readonly VoicePool _voicePool;

    public ObservableCollection<ExtensionItem> Items { get; } = new();
    public ObservableCollection<SettingFieldViewModel> SettingFields { get; } = new();

    public bool HasSettings => SettingFields.Count > 0;

    private ExtensionItem? _selected;
    public ExtensionItem? Selected
    {
        get => _selected;
        set { SetField(ref _selected, value); RefreshSettings(); }
    }

    public RelayCommand ReloadCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand OpenExtensionsFolderCommand { get; }
    public RelayCommand SaveSettingsCommand { get; }

    public ExtensionsViewModel(ExtensionManager extensions, TtsEngineRegistry registry, VoicePool voicePool)
    {
        _extensions = extensions;
        _registry = registry;
        _voicePool = voicePool;

        ReloadCommand = new RelayCommand(Reload);
        OpenFolderCommand = new RelayCommand(OpenFolder, () => Selected != null);
        OpenExtensionsFolderCommand = new RelayCommand(OpenExtensionsFolder);
        SaveSettingsCommand = new RelayCommand(SaveSettings, () => HasSettings);

        Refresh();
    }

    private void Refresh()
    {
        Items.Clear();
        foreach (var ext in _extensions.Extensions)
        {
            var cards = new List<ExtensionInfoCard>();
            foreach (var engine in ext.SpeechEngines)
                cards.Add(new ExtensionInfoCard
                {
                    Header = "Speech Engine",
                    Body = $"Go to Speech Engines → Add and select \"{engine.DisplayName}\" to configure and enable it."
                });

            Items.Add(new ExtensionItem
            {
                EngineId = ext.ExtensionId,
                DisplayName = ext.DisplayName,
                Description = ext.Description,
                FolderPath = Path.Combine(ExtensionManager.ExtensionsDirectory,
                    ext.ExtensionId.Replace("ext:", string.Empty)),
                InfoCards = cards,
                Status = "Loaded"
            });
        }
        Selected = Items.FirstOrDefault();
    }

    private void RefreshSettings()
    {
        SettingFields.Clear();
        if (_selected == null) return;

        var ext = _extensions.Extensions.FirstOrDefault(e => e.ExtensionId == _selected.EngineId);
        if (ext == null) return;

        foreach (var field in ext.SettingFields)
        {
            SettingFields.Add(new SettingFieldViewModel
            {
                Key = field.Key,
                Label = field.Label,
                Type = field.Type,
                Options = field.Options,
                Value = ext.GetSettingValue(field.Key)
            });
        }

        OnPropertyChanged(nameof(HasSettings));
    }

    private void SaveSettings()
    {
        if (_selected == null) return;
        var values = SettingFields.ToDictionary(f => f.Key, f => f.Value);
        _extensions.SaveSettings(_selected.EngineId, values);
    }

    private void Reload()
    {
        _extensions.Reload();
        _registry.Reload();
        _voicePool.Invalidate();
        Refresh();
    }

    private void OpenFolder()
    {
        if (Selected == null) return;
        if (Directory.Exists(Selected.FolderPath))
            System.Diagnostics.Process.Start("explorer.exe", Selected.FolderPath);
    }

    private void OpenExtensionsFolder()
    {
        Directory.CreateDirectory(ExtensionManager.ExtensionsDirectory);
        System.Diagnostics.Process.Start("explorer.exe", ExtensionManager.ExtensionsDirectory);
    }
}
