using System.Collections.ObjectModel;
using OpenSpeaker.Models;
namespace OpenSpeaker.ViewModels;

public class ApiHeaderRow : BaseViewModel
{
    private string _key = string.Empty;
    private string _value = string.Empty;
    public string Key { get => _key; set => SetField(ref _key, value); }
    public string Value { get => _value; set => SetField(ref _value, value); }
}

public class CustomApiEditViewModel : BaseViewModel
{
    private string _name = string.Empty;
    private bool _enabled = true;
    private string _synthUrl = string.Empty;
    private string _synthMethod = "POST";
    private string _synthBodyTemplate = "{\"text\": \"{text}\", \"voice\": \"{voice}\"}";
    private string _responseFormat = "binary";
    private string _responseAudioPath = string.Empty;
    private string _audioFormat = "mp3";
    private string _voicesUrl = string.Empty;
    private string _voicesMethod = "GET";
    private string _voicesArrayPath = string.Empty;
    private string _voiceIdField = "id";
    private string _voiceNameField = "name";

    public string Name { get => _name; set => SetField(ref _name, value); }
    public bool Enabled { get => _enabled; set => SetField(ref _enabled, value); }
    public string SynthUrl { get => _synthUrl; set => SetField(ref _synthUrl, value); }
    public string SynthMethod { get => _synthMethod; set => SetField(ref _synthMethod, value); }
    public string SynthBodyTemplate { get => _synthBodyTemplate; set => SetField(ref _synthBodyTemplate, value); }
    public string ResponseFormat
    {
        get => _responseFormat;
        set { SetField(ref _responseFormat, value); OnPropertyChanged(nameof(IsJsonResponse)); }
    }
    public bool IsJsonResponse => _responseFormat != "binary";
    public string ResponseAudioPath { get => _responseAudioPath; set => SetField(ref _responseAudioPath, value); }
    public string AudioFormat { get => _audioFormat; set => SetField(ref _audioFormat, value); }
    public string VoicesUrl { get => _voicesUrl; set => SetField(ref _voicesUrl, value); }
    public string VoicesMethod { get => _voicesMethod; set => SetField(ref _voicesMethod, value); }
    public string VoicesArrayPath { get => _voicesArrayPath; set => SetField(ref _voicesArrayPath, value); }
    public string VoiceIdField { get => _voiceIdField; set => SetField(ref _voiceIdField, value); }
    public string VoiceNameField { get => _voiceNameField; set => SetField(ref _voiceNameField, value); }

    public ObservableCollection<ApiHeaderRow> SynthHeaders { get; } = new();
    public ObservableCollection<ApiHeaderRow> VoicesHeaders { get; } = new();

    public IEnumerable<string> Methods { get; } = new[] { "POST", "GET" };
    public IEnumerable<string> ResponseFormats { get; } = new[] { "binary", "base64", "url" };
    public IEnumerable<string> AudioFormats { get; } = new[] { "mp3", "wav" };

    public RelayCommand AddSynthHeaderCommand { get; }
    public RelayCommand RemoveSynthHeaderCommand { get; }
    public RelayCommand AddVoicesHeaderCommand { get; }
    public RelayCommand RemoveVoicesHeaderCommand { get; }

    public CustomApiEditViewModel()
    {
        AddSynthHeaderCommand = new RelayCommand(() => SynthHeaders.Add(new ApiHeaderRow()));
        RemoveSynthHeaderCommand = new RelayCommand(p => { if (p is ApiHeaderRow r) SynthHeaders.Remove(r); });
        AddVoicesHeaderCommand = new RelayCommand(() => VoicesHeaders.Add(new ApiHeaderRow()));
        RemoveVoicesHeaderCommand = new RelayCommand(p => { if (p is ApiHeaderRow r) VoicesHeaders.Remove(r); });
    }

    public void LoadFrom(CustomApiDefinition def)
    {
        Name = def.Name;
        Enabled = def.Enabled;
        SynthUrl = def.SynthUrl;
        SynthMethod = def.SynthMethod;
        SynthBodyTemplate = def.SynthBodyTemplate;
        ResponseFormat = def.ResponseFormat;
        ResponseAudioPath = def.ResponseAudioPath;
        AudioFormat = def.AudioFormat;
        VoicesUrl = def.VoicesUrl;
        VoicesMethod = def.VoicesMethod;
        VoicesArrayPath = def.VoicesArrayPath;
        VoiceIdField = def.VoiceIdField;
        VoiceNameField = def.VoiceNameField;
        SynthHeaders.Clear();
        foreach (var h in def.SynthHeaders) SynthHeaders.Add(new ApiHeaderRow { Key = h.Key, Value = h.Value });
        VoicesHeaders.Clear();
        foreach (var h in def.VoicesHeaders) VoicesHeaders.Add(new ApiHeaderRow { Key = h.Key, Value = h.Value });
    }

    public void ApplyTo(CustomApiDefinition def)
    {
        def.Name = Name;
        def.Enabled = Enabled;
        def.SynthUrl = SynthUrl;
        def.SynthMethod = SynthMethod;
        def.SynthBodyTemplate = SynthBodyTemplate;
        def.ResponseFormat = ResponseFormat;
        def.ResponseAudioPath = ResponseAudioPath;
        def.AudioFormat = AudioFormat;
        def.VoicesUrl = VoicesUrl;
        def.VoicesMethod = VoicesMethod;
        def.VoicesArrayPath = VoicesArrayPath;
        def.VoiceIdField = VoiceIdField;
        def.VoiceNameField = VoiceNameField;
        def.SynthHeaders = SynthHeaders.Select(h => new ApiHeader { Key = h.Key, Value = h.Value }).ToList();
        def.VoicesHeaders = VoicesHeaders.Select(h => new ApiHeader { Key = h.Key, Value = h.Value }).ToList();
    }
}
