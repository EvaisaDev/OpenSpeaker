using System.Collections.ObjectModel;
using OpenSpeaker.Audio;
using OpenSpeaker.ThingsIDKWhereToPut.Logging;
using OpenSpeaker.Models;
using OpenSpeaker.Services;
using OpenSpeaker.TTS;
namespace OpenSpeaker.ViewModels;

public record VoiceItem(string DisplayName, string EngineId, string VoiceId)
{
    public override string ToString() => DisplayName;
}

public class GenericSpeakerViewModel : BaseViewModel
{
    private readonly TtsEngineRegistry _engineRegistry;
    private readonly VoicePool _voicePool;
    private readonly NAudioPlayer _player;
    private readonly IAppLogger? _logger;

    private List<VoiceItem> _allVoices = new();
    private ObservableCollection<VoiceItem> _voices = new();
    public ObservableCollection<VoiceItem> Voices { get => _voices; private set { _voices = value; OnPropertyChanged(); } }
    public ObservableCollection<AliasParamRow> ParamRows { get; } = new();
    public ObservableCollection<string> SpokenPreviews { get; } = new();
    public event EventHandler? VoicesLoaded;

    private string _voiceFilter = "";
    public string VoiceFilter
    {
        get => _voiceFilter;
        set
        {
            SetField(ref _voiceFilter, value);
            Voices = new ObservableCollection<VoiceItem>(
                string.IsNullOrEmpty(value) ? _allVoices
                : _allVoices.Where(v => v.DisplayName.Contains(value, StringComparison.OrdinalIgnoreCase)));
        }
    }

    private VoiceItem? _selectedVoice;
    public VoiceItem? SelectedVoice
    {
        get => _selectedVoice;
        set
        {
            SetField(ref _selectedVoice, value);
            if (value != null && !string.IsNullOrEmpty(_voiceFilter))
                VoiceFilter = "";
            RebuildParamRows();
        }
    }

    private int _volume = 100;
    public int Volume { get => _volume; set => SetField(ref _volume, value); }

    private string _text = "This is a test message";
    public string Text { get => _text; set => SetField(ref _text, value); }

    private string _status = "Loading voices...";
    public string Status { get => _status; set => SetField(ref _status, value); }

    public AsyncRelayCommand SpeakCommand { get; }
    public RelayCommand ResetVolumeCommand { get; }

    public GenericSpeakerViewModel(TtsEngineRegistry engineRegistry, VoicePool voicePool, IAppLogger? logger = null)
    {
        _engineRegistry = engineRegistry;
        _voicePool = voicePool;
        _player = new NAudioPlayer();
        _logger = logger;

        SpeakCommand = new AsyncRelayCommand(SpeakAsync);
        ResetVolumeCommand = new RelayCommand(() => Volume = 100);

        _ = LoadVoicesAsync();
    }

    private async Task LoadVoicesAsync()
    {
        var all = await _voicePool.GetAllAsync();
        Application.Current?.Dispatcher.Invoke(() =>
        {
            _allVoices    = all.Select(((string engineId, VoiceInfo voice) x) =>
            {
                var locale = x.voice.Locale ?? string.Empty;
                var name = x.voice.Name;
                if (!string.IsNullOrEmpty(locale) && !name.Contains(locale, StringComparison.OrdinalIgnoreCase))
                    name = $"{name} ({locale})";
                return new VoiceItem($"{_engineRegistry.GetDisplayName(x.engineId)}: {name}", x.engineId, x.voice.Id);
            }).ToList();
            Voices        = new ObservableCollection<VoiceItem>(_allVoices);
            SelectedVoice = Voices.FirstOrDefault();
            Status        = Voices.Count > 0 ? "Waiting..." : "No voices loaded.";
            VoicesLoaded?.Invoke(this, EventArgs.Empty);
        });
    }

    private void RebuildParamRows()
    {
        ParamRows.Clear();
        if (_selectedVoice == null) return;
        var engine = _engineRegistry.GetEngine(_selectedVoice.EngineId);
        foreach (var def in engine?.GetParameters() ?? Array.Empty<EngineParameterDef>())
            ParamRows.Add(new AliasParamRow { Def = def, Value = def.Default });
    }

    private async Task SpeakAsync()
    {
        if (string.IsNullOrWhiteSpace(Text) || SelectedVoice == null) return;

        Status = "Speaking...";
        try
        {
            var engine = _engineRegistry.GetEngine(SelectedVoice.EngineId) ?? _engineRegistry.GetDefaultEngine();
            var paramDict = ParamRows.ToDictionary(r => r.Def.Key, r => r.Value);
            var audio = await engine.SynthesizeAsync(Text, SelectedVoice.VoiceId, new SynthParams(paramDict));

            if (!audio.IsEmpty)
            {
                await _player.PlayAsync(audio, string.Empty, Volume);
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    SpokenPreviews.Insert(0, $"[{SelectedVoice.DisplayName}] {Text}");
                    if (SpokenPreviews.Count > 100)
                        SpokenPreviews.RemoveAt(SpokenPreviews.Count - 1);
                });
            }

            Status = "Done.";
        }
        catch (Exception ex)
        {
            _logger?.Error($"Generic speaker error: {ex.Message}");
            Status = $"Error: {ex.Message}";
        }
    }
}
