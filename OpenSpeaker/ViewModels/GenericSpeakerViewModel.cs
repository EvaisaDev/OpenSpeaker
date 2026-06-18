using System.Collections.ObjectModel;
using OpenSpeaker.Data;
using OpenSpeaker.Models;
using OpenSpeaker.TTS;
using OpenSpeaker.Audio;
namespace OpenSpeaker.ViewModels;

public class GenericSpeakerViewModel : BaseViewModel
{
    private readonly TtsEngineRegistry _engineRegistry;
    private readonly VoiceAliasRepository _aliasRepo;
    private readonly NAudioPlayer _player;

    public ObservableCollection<string> AliasNames { get; } = new();
    public ObservableCollection<string> SpokenPreviews { get; } = new();

    private string _selectedAlias = string.Empty;
    public string SelectedAlias { get => _selectedAlias; set => SetField(ref _selectedAlias, value); }

    private int _pitch = 0;
    public int Pitch { get => _pitch; set => SetField(ref _pitch, value); }

    private int _rate = 0;
    public int Rate { get => _rate; set => SetField(ref _rate, value); }

    private int _volume = 100;
    public int Volume { get => _volume; set => SetField(ref _volume, value); }

    private string _text = "This is a test message";
    public string Text { get => _text; set => SetField(ref _text, value); }

    private string _status = "Waiting...";
    public string Status { get => _status; set => SetField(ref _status, value); }

    public AsyncRelayCommand SpeakCommand { get; }
    public RelayCommand DefaultCommand { get; }

    public GenericSpeakerViewModel(TtsEngineRegistry engineRegistry, VoiceAliasRepository aliasRepo)
    {
        _engineRegistry = engineRegistry;
        _aliasRepo = aliasRepo;
        _player = new NAudioPlayer();

        AliasNames.Add("Random Voice");
        foreach (var a in aliasRepo.GetAllSorted())
            AliasNames.Add(a.Name);
        _selectedAlias = AliasNames.FirstOrDefault() ?? "Random Voice";

        SpeakCommand = new AsyncRelayCommand(SpeakAsync);
        DefaultCommand = new RelayCommand(ResetDefaults);
    }

    private async Task SpeakAsync()
    {
        if (string.IsNullOrWhiteSpace(Text)) return;

        Status = "Speaking...";
        try
        {
            VoiceAlias? alias = null;
            if (_selectedAlias != "Random Voice")
                alias = _aliasRepo.GetAllSorted().FirstOrDefault(a => a.Name == _selectedAlias);

            var engine = alias != null
                ? _engineRegistry.GetEngine(alias.EngineId) ?? _engineRegistry.GetDefaultEngine()
                : _engineRegistry.GetDefaultEngine();

            var voiceId = alias?.VoiceId ?? string.Empty;
            var synthParams = SynthParams.FromJson(alias?.EngineParamsJson);
            var audio = await engine.SynthesizeAsync(Text, voiceId, synthParams);

            if (!audio.IsEmpty)
            {
                var deviceId = alias?.OutputDeviceId ?? string.Empty;
                await _player.PlayAsync(audio, deviceId, Volume);
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    SpokenPreviews.Insert(0, $"[{_selectedAlias}] {Text}");
                    if (SpokenPreviews.Count > 100)
                        SpokenPreviews.RemoveAt(SpokenPreviews.Count - 1);
                });
            }

            Status = "Done.";
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
    }

    private void ResetDefaults()
    {
        Pitch = 0;
        Rate = 0;
        Volume = 100;
    }
}
