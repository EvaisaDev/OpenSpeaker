using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Extensions;
using OpenSpeaker.Models;
using OpenSpeaker.TTS;
namespace OpenSpeaker.ViewModels;

public class EngineConfigField : BaseViewModel
{
    private string _value = string.Empty;
    private bool _isVisible = true;
    public string Label { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public bool IsFilePath { get; init; }
    public bool IsDropdown { get; init; }
    public IReadOnlyList<string> Options { get; init; } = Array.Empty<string>();
    public string FileFilter { get; init; } = "All files|*.*";
    public string Value { get => _value; set => SetField(ref _value, value); }
    public bool IsVisible { get => _isVisible; set => SetField(ref _isVisible, value); }
}

public record EngineOption(string Id, string DisplayName);

public class EngineConfigViewModel : BaseViewModel
{
    private EngineOption? _selectedEngine;
    private readonly Action _close;
    private readonly ExtensionManager? _extensions;
    private readonly IDialogService _dialogs;

    public ObservableCollection<EngineOption> Engines { get; } = new();
    public ObservableCollection<EngineConfigField> Fields { get; } = new();
    public bool Confirmed { get; private set; }

    public EngineOption? SelectedEngine
    {
        get => _selectedEngine;
        set { SetField(ref _selectedEngine, value); UpdateFields(); }
    }

    public RelayCommand OkCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand BrowseFileCommand { get; }

    public EngineConfigViewModel(IEnumerable<EngineOption> engines, Action close, ExtensionManager? extensions = null, IDialogService? dialogs = null)
    {
        _close = close;
        _extensions = extensions;
        _dialogs = dialogs ?? new DialogService();

        foreach (var opt in engines)
            Engines.Add(opt);

        OkCommand = new RelayCommand(() => { Confirmed = true; _close(); });
        CancelCommand = new RelayCommand(_close);
        BrowseFileCommand = new RelayCommand(p =>
        {
            if (p is not EngineConfigField field) return;
            var path = _dialogs.PickFile(field.FileFilter);
            if (path != null) field.Value = path;
        });

        SelectedEngine = Engines.FirstOrDefault();
    }

    private void UpdateFields()
    {
        foreach (var f in Fields)
            f.PropertyChanged -= OnFieldChanged;
        Fields.Clear();
        if (_selectedEngine == null) return;
        foreach (var field in GetFields(_selectedEngine.Id))
            Fields.Add(field);
        foreach (var f in Fields)
            f.PropertyChanged += OnFieldChanged;
    }

    private void OnFieldChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(EngineConfigField.Value) || sender is not EngineConfigField f) return;
        if (f.Key == "__mode__" && _selectedEngine?.Id == EngineIds.TtsMonster)
            RefreshTtsMonsterVisibility(f.Value);
    }

    private void RefreshTtsMonsterVisibility(string mode)
    {
        var overlayField = Fields.FirstOrDefault(f => f.Key == "overlayUrl");
        var apiField = Fields.FirstOrDefault(f => f.Key == "apiToken");
        if (overlayField != null) overlayField.IsVisible = mode == "Overlay URL";
        if (apiField != null) apiField.IsVisible = mode != "Overlay URL";
    }

    public string BuildConfigJson()
    {
        var obj = new JObject();
        foreach (var f in Fields)
        {
            if (f.Key.StartsWith("__", StringComparison.Ordinal)) continue;
            if (!f.IsVisible) continue;
            obj[f.Key] = f.Value;
        }
        return obj.ToString(Newtonsoft.Json.Formatting.None);
    }

    public static string GetDisplayName(string engineId) =>
        TtsEngineFactory.GetDisplayName(engineId);

    private IEnumerable<EngineConfigField> GetFields(string engineId)
    {
        if (engineId.StartsWith("ext:", StringComparison.Ordinal) && _extensions != null)
            return _extensions.GetAuthFields(engineId)
                .Select(f => new EngineConfigField { Label = f.Label, Key = f.Key });

        return engineId switch
        {
            EngineIds.ElevenLabs  => [new() { Label = "API Key",              Key = "apiKey" }],
            EngineIds.CereProc    => [new() { Label = "Username", Key = "username" },
                                       new() { Label = "Password", Key = "password" }],
            EngineIds.TtsMonster  => [new() { Label = "Auth Mode", Key = "__mode__", IsDropdown = true, Options = ["Overlay URL", "Api Key"], Value = "Overlay URL" },
                                       new() { Label = "Overlay URL", Key = "overlayUrl" },
                                       new() { Label = "API Token", Key = "apiToken", IsVisible = false }],
            EngineIds.GoogleCloud => [new() { Label = "Service Account JSON", Key = "serviceAccountJsonPath", IsFilePath = true, FileFilter = "JSON files|*.json|All files|*.*" }],
            EngineIds.Azure       => [new() { Label = "Subscription Key",     Key = "subscriptionKey" },
                                       new() { Label = "Region",               Key = "region" }],
            EngineIds.IbmWatson   => [new() { Label = "Credentials .env", Key = "envFilePath", IsFilePath = true, FileFilter = ".env files|*.env|All files|*.*" }],
            EngineIds.UberDuck    => [new() { Label = "API Key",              Key = "apiKey" }],
            EngineIds.AmazonPolly => [new() { Label = "Access Key ID",        Key = "accessKey" },
                                       new() { Label = "Secret Access Key",    Key = "secretKey" },
                                       new() { Label = "Region",               Key = "region", Value = "us-east-1" }],
            EngineIds.Acapela     => [new() { Label = "Account",              Key = "account" },
                                       new() { Label = "Password",             Key = "password" },
                                       new() { Label = "Application Name",     Key = "applicationName", Value = "OpenSpeaker" }],
            EngineIds.TikTok      => [],
            // EngineIds.FakeYou     => [new() { Label = "Username (optional)", Key = "username" },
            //                            new() { Label = "Password (optional)", Key = "password" }],
            EngineIds.FishAudio   => [new() { Label = "API Key", Key = "apiKey" }],
            EngineIds.Inworld     => [new() { Label = "API Key", Key = "apiKey" }],
            EngineIds.Resemble    => [new() { Label = "API Key", Key = "apiKey" }],
            EngineIds.Cartesia    => [new() { Label = "API Key", Key = "apiKey" }],
            EngineIds.Lmnt        => [new() { Label = "API Key", Key = "apiKey" }],
            _                     => []
        };
    }
}
