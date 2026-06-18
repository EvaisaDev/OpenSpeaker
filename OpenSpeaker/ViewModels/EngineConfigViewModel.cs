using System.Collections.ObjectModel;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Models;
namespace OpenSpeaker.ViewModels;

public class EngineConfigField : BaseViewModel
{
    private string _value = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public bool IsFilePath { get; init; }
    public string Value { get => _value; set => SetField(ref _value, value); }
}

public record EngineOption(string Id, string DisplayName);

public class EngineConfigViewModel : BaseViewModel
{
    private EngineOption? _selectedEngine;
    private readonly Action _close;

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

    public EngineConfigViewModel(IEnumerable<string> availableEngineIds, Action close)
    {
        _close = close;

        foreach (var id in availableEngineIds)
            Engines.Add(new EngineOption(id, GetDisplayName(id)));

        OkCommand = new RelayCommand(() => { Confirmed = true; _close(); });
        CancelCommand = new RelayCommand(_close);
        BrowseFileCommand = new RelayCommand(p =>
        {
            if (p is not EngineConfigField field) return;
            var dialog = new OpenFileDialog { Filter = "JSON files|*.json|All files|*.*" };
            if (dialog.ShowDialog() == DialogResult.OK)
                field.Value = dialog.FileName;
        });

        SelectedEngine = Engines.FirstOrDefault();
    }

    private void UpdateFields()
    {
        Fields.Clear();
        if (_selectedEngine == null) return;
        foreach (var field in GetFields(_selectedEngine.Id))
            Fields.Add(field);
    }

    public string BuildConfigJson()
    {
        var obj = new JObject();
        foreach (var f in Fields)
            obj[f.Key] = f.Value;
        return obj.ToString(Newtonsoft.Json.Formatting.None);
    }

    public static string GetDisplayName(string engineId) => engineId switch
    {
        EngineIds.Azure       => "Azure Cognitive Services",
        EngineIds.AmazonPolly => "Amazon Polly",
        EngineIds.GoogleCloud => "Google Cloud TTS",
        EngineIds.ElevenLabs  => "ElevenLabs.io",
        EngineIds.TtsMonster  => "TTSMonster",
        EngineIds.TikTok      => "TikTok TTS",
        EngineIds.IbmWatson   => "IBM Watson TTS",
        EngineIds.Acapela     => "Acapela Cloud",
        EngineIds.CereProc    => "CereProc Web Services",
        EngineIds.UberDuck    => "Uberduck",
        EngineIds.Sapi5       => "SAPI5",
        _                     => engineId
    };

    private static IEnumerable<EngineConfigField> GetFields(string engineId) => engineId switch
    {
        EngineIds.ElevenLabs  => [new() { Label = "API Key",              Key = "apiKey" }],
        EngineIds.CereProc    => [new() { Label = "API Key",              Key = "apiKey" }],
        EngineIds.TtsMonster  => [new() { Label = "API Token (ttsm_...)", Key = "apiToken" },
                                   new() { Label = "Overlay URL (legacy)", Key = "overlayUrl" }],
        EngineIds.GoogleCloud => [new() { Label = "Service Account JSON", Key = "serviceAccountJsonPath", IsFilePath = true }],
        EngineIds.Azure       => [new() { Label = "Subscription Key",     Key = "subscriptionKey" },
                                   new() { Label = "Region",               Key = "region" }],
        EngineIds.IbmWatson   => [new() { Label = "API Key",              Key = "apiKey" },
                                   new() { Label = "Service URL",          Key = "serviceUrl" }],
        EngineIds.UberDuck    => [new() { Label = "API Key",              Key = "apiKey" },
                                   new() { Label = "API Secret",           Key = "apiSecret" }],
        EngineIds.AmazonPolly => [new() { Label = "Access Key ID",        Key = "accessKey" },
                                   new() { Label = "Secret Access Key",    Key = "secretKey" },
                                   new() { Label = "Region",               Key = "region", Value = "us-east-1" }],
        EngineIds.Acapela     => [new() { Label = "Account",              Key = "account" },
                                   new() { Label = "Password",             Key = "password" },
                                   new() { Label = "Application Name",     Key = "applicationName", Value = "OpenSpeaker" }],
        _                     => []
    };
}
