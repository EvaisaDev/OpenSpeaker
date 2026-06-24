using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using OpenSpeaker.TTS;
namespace OpenSpeaker.ViewModels;

public class AliasParamRow : BaseViewModel
{
    public EngineParameterDef Def { get; init; } = null!;

    private string _value = string.Empty;
    public string Value
    {
        get => _value;
        set
        {
            SetField(ref _value, value);
            OnPropertyChanged(nameof(SliderValue));
            OnPropertyChanged(nameof(DisplayValue));
        }
    }

    public bool IsSlider => Def.Type == EngineParameterType.Slider;
    public bool IsComboBox => Def.Type == EngineParameterType.ComboBox;
    public bool IsSearchableVoice => Def.Type == EngineParameterType.SearchableVoice;

    public double SliderValue
    {
        get => double.TryParse(_value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
        set => Value = value.ToString("G4", CultureInfo.InvariantCulture);
    }

    public string DisplayValue
    {
        get
        {
            if (!double.TryParse(_value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return _value;
            return Def.Step >= 1 ? v.ToString("0") : v.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }

    private const int SearchLimit = 100;

    private IVoiceSearchEngine? _search;
    public ObservableCollection<VoiceInfo> VoiceOptions { get; } = new();

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetField(ref _searchText, value)) return;
            _ = RunSearchAsync(value);
        }
    }

    private VoiceInfo? _selectedVoiceOption;
    public VoiceInfo? SelectedVoiceOption
    {
        get => _selectedVoiceOption;
        set
        {
            SetField(ref _selectedVoiceOption, value);
            if (value != null) Value = value.Id;
        }
    }

    public void AttachSearch(IVoiceSearchEngine search)
    {
        _search = search;
        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        if (_search == null) return;

        var top = await _search.TopVoicesAsync(SearchLimit);
        SetOptions(top);

        if (!string.IsNullOrEmpty(_value))
        {
            var current = await _search.ResolveVoiceAsync(_value);
            if (current != null)
            {
                if (VoiceOptions.All(v => v.Id != current.Id))
                    VoiceOptions.Insert(0, current);
                SetField(ref _selectedVoiceOption, VoiceOptions.First(v => v.Id == current.Id), nameof(SelectedVoiceOption));
            }
        }
    }

    private CancellationTokenSource? _searchCts;
    private async Task RunSearchAsync(string query)
    {
        if (_search == null) return;
        _searchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchCts = cts;
        try
        {
            await Task.Delay(300, cts.Token);
            var results = await _search.SearchVoicesAsync(query, SearchLimit);
            if (!cts.IsCancellationRequested) SetOptions(results);
        }
        catch (TaskCanceledException) { }
    }

    private void SetOptions(IReadOnlyList<VoiceInfo> voices)
    {
        VoiceOptions.Clear();
        foreach (var v in voices) VoiceOptions.Add(v);
    }
}
