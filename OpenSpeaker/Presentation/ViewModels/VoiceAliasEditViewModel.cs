using OpenSpeaker.Audio;
using OpenSpeaker.Models;
using OpenSpeaker.TTS;
namespace OpenSpeaker.ViewModels;

public class VoiceAliasEditViewModel : BaseViewModel
{
    private readonly TtsEngineRegistry _engineRegistry;
    private readonly AudioDeviceEnumerator _deviceEnumerator;

    private string _name = string.Empty;
    private string _engineId = EngineIds.Sapi5;
    private string _voiceId = string.Empty;
    private int _volume = 100;
    private string _outputDeviceId = string.Empty;
    private IReadOnlyList<VoiceInfo> _availableVoices = Array.Empty<VoiceInfo>();

    public string Name { get => _name; set => SetField(ref _name, value); }
    public string EngineId { get => _engineId; set { if (SetField(ref _engineId, value)) _ = LoadVoicesAsync(); } }
    public string VoiceId { get => _voiceId; set => SetField(ref _voiceId, value); }
    public int Volume { get => _volume; set => SetField(ref _volume, value); }
    public string OutputDeviceId { get => _outputDeviceId; set => SetField(ref _outputDeviceId, value); }
    public IReadOnlyList<VoiceInfo> AvailableVoices { get => _availableVoices; private set => SetField(ref _availableVoices, value); }

    public IReadOnlyList<AudioDeviceInfo> OutputDevices { get; }
    public IEnumerable<string> AvailableEngineIds { get; } = new[]
    {
        EngineIds.Sapi5, EngineIds.Azure, EngineIds.AmazonPolly, EngineIds.GoogleCloud,
        EngineIds.ElevenLabs, EngineIds.TtsMonster, EngineIds.IbmWatson,
        EngineIds.Acapela, EngineIds.CereProc, EngineIds.UberDuck
    };

    public AsyncRelayCommand LoadVoicesCommand { get; }

    public VoiceAliasEditViewModel(TtsEngineRegistry engineRegistry, AudioDeviceEnumerator deviceEnumerator)
    {
        _engineRegistry = engineRegistry;
        _deviceEnumerator = deviceEnumerator;
        OutputDevices = deviceEnumerator.GetOutputDevices();
        LoadVoicesCommand = new AsyncRelayCommand(LoadVoicesAsync);
    }

    public void LoadFrom(VoiceAlias alias)
    {
        Name = alias.Name;
        EngineId = alias.EngineId;
        VoiceId = alias.VoiceId;
        Volume = alias.Volume;
        var deviceId = alias.OutputDeviceId ?? string.Empty;
        if (OutputDevices.Count > 0 && !OutputDevices.Any(d => d.Id == deviceId))
            deviceId = OutputDevices[0].Id;
        OutputDeviceId = deviceId;
    }

    public VoiceAlias ToModel(VoiceAlias? existing = null)
    {
        var alias = existing ?? new VoiceAlias();
        alias.Name = Name;
        alias.EngineId = EngineId;
        alias.VoiceId = VoiceId;
        alias.Volume = Volume;
        alias.OutputDeviceId = OutputDeviceId;
        return alias;
    }

    private async Task LoadVoicesAsync()
    {
        var engine = _engineRegistry.GetEngine(EngineId);
        if (engine == null) { AvailableVoices = Array.Empty<VoiceInfo>(); return; }
        AvailableVoices = await engine.GetVoicesAsync();
    }
}
