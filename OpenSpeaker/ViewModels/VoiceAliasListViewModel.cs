using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using OpenSpeaker.Audio;
using OpenSpeaker.Data;
using OpenSpeaker.Infrastructure.Logging;
using OpenSpeaker.Models;
using OpenSpeaker.TTS;
namespace OpenSpeaker.ViewModels;

public class AliasUserNode
{
    public string Label { get; init; } = string.Empty;
    public List<AliasUserNode> Children { get; init; } = new();
}

public class VoiceAliasListViewModel : BaseViewModel
{
    private readonly VoiceAliasRepository _repo;
    private readonly TtsEngineRegistry _engineRegistry;
    private readonly AudioDeviceEnumerator _deviceEnumerator;
    private readonly UserRepository _userRepo;
    private readonly IAppLogger? _logger;

    public static readonly VoiceInfo RandomVoiceSentinel = new() { Id = string.Empty, Name = "Random Voice" };

    public ObservableCollection<VoiceAlias> Aliases { get; } = new();
    public ObservableCollection<AudioDeviceInfo> OutputDevices { get; } = new();
    public ObservableCollection<VoiceInfo> AvailableVoices { get; } = new();
    public ObservableCollection<EngineOption> AvailableEngines { get; } = new();
    public ObservableCollection<AliasUserNode> InUseTree { get; } = new();
    public ObservableCollection<string> InUseUsers { get; } = new();
    public ObservableCollection<VoiceInfo> SpeakVoices { get; } = new();
    public ObservableCollection<VoiceInfo> AliasVoices { get; } = new();
    public ObservableCollection<AliasParamRow> ParamRows { get; } = new();

    private VoiceAlias? _selectedAlias;
    public VoiceAlias? SelectedAlias
    {
        get => _selectedAlias;
        set { SetField(ref _selectedAlias, value); OnPropertyChanged(nameof(HasSelected)); LoadAliasDetail(); }
    }

    public bool HasSelected => _selectedAlias != null;

    private string _detailName = string.Empty;
    public string DetailName { get => _detailName; set => SetField(ref _detailName, value); }

    private string _detailEngineId = EngineIds.Sapi5;
    public string DetailEngineId
    {
        get => _detailEngineId;
        set
        {
            if (SetField(ref _detailEngineId, value))
                RebuildParamRows();
        }
    }

    private string _detailOutputDeviceId = string.Empty;
    public string DetailOutputDeviceId
    {
        get => _detailOutputDeviceId;
        set
        {
            if (SetField(ref _detailOutputDeviceId, value) && _selectedAlias != null)
            {
                _selectedAlias.OutputDeviceId = value;
                _repo.Upsert(_selectedAlias);
            }
        }
    }

    private string _testMessage = "This is a test message";
    public string TestMessage { get => _testMessage; set => SetField(ref _testMessage, value); }

    private int _testVolume = 100;
    public int TestVolume { get => _testVolume; set => SetField(ref _testVolume, value); }

    private string _newAliasName = string.Empty;
    public string NewAliasName { get => _newAliasName; set => SetField(ref _newAliasName, value); }

    private VoiceInfo? _testSpeakVoice = RandomVoiceSentinel;
    public VoiceInfo? TestSpeakVoice
    {
        get => _testSpeakVoice;
        set
        {
            if (!SetField(ref _testSpeakVoice, value)) return;
            if (value != null && value != RandomVoiceSentinel)
            {
                if (value.EngineId is { Length: > 0 } eid)
                {
                    _detailEngineId = eid;
                    OnPropertyChanged(nameof(DetailEngineId));
                }
                RebuildParamRows();
                var inList = AliasVoices.FirstOrDefault(v => v.Id == value.Id);
                if (inList != null && _selectedVoice?.Id != inList.Id)
                {
                    _selectedVoice = inList;
                    OnPropertyChanged(nameof(SelectedVoice));
                }
            }
        }
    }

    private VoiceInfo? _selectedVoice;
    public VoiceInfo? SelectedVoice
    {
        get => _selectedVoice;
        set
        {
            if (!SetField(ref _selectedVoice, value)) return;
            if (value == null) return;
            TestSpeakVoice = value;
            if (value.EngineId is { Length: > 0 } eid)
            {
                _detailEngineId = eid;
                OnPropertyChanged(nameof(DetailEngineId));
            }
            RebuildParamRows();
        }
    }

    public RelayCommand AddAliasCommand { get; }
    public RelayCommand DeleteAliasCommand { get; }
    public RelayCommand RenameCommand { get; }
    public RelayCommand SaveAliasDetailCommand { get; }
    public RelayCommand AddVoiceCommand { get; }
    public RelayCommand RemoveVoiceCommand { get; }
    public RelayCommand RemoveAllVoicesCommand { get; }
    public RelayCommand SetDefaultVoiceCommand { get; }
    public AsyncRelayCommand TestSpeakCommand { get; }
    public AsyncRelayCommand LoadVoicesCommand { get; }

    public VoiceAliasListViewModel(VoiceAliasRepository repo, TtsEngineRegistry engineRegistry, AudioDeviceEnumerator deviceEnumerator, UserRepository userRepo, IAppLogger? logger = null)
    {
        _repo = repo;
        _engineRegistry = engineRegistry;
        _deviceEnumerator = deviceEnumerator;
        _userRepo = userRepo;
        _logger = logger;

        AddAliasCommand = new RelayCommand(AddAlias, () => !string.IsNullOrWhiteSpace(DetailName));
        DeleteAliasCommand = new RelayCommand(DeleteAlias, () => SelectedAlias != null);
        RenameCommand = new RelayCommand(Rename, () => SelectedAlias != null && !string.IsNullOrWhiteSpace(DetailName));
        SaveAliasDetailCommand = new RelayCommand(SaveAliasDetail, () => SelectedAlias != null);
        AddVoiceCommand = new RelayCommand(AddVoice, () => SelectedAlias != null && TestSpeakVoice?.Id is { Length: > 0 });
        RemoveVoiceCommand = new RelayCommand(RemoveVoice, () => SelectedAlias != null && _selectedVoice != null);
        RemoveAllVoicesCommand = new RelayCommand(RemoveAllVoices, () => SelectedAlias != null && AliasVoices.Count > 0);
        SetDefaultVoiceCommand = new RelayCommand(SetDefaultVoice, () => SelectedAlias != null && TestSpeakVoice?.Id is { Length: > 0 });
        TestSpeakCommand = new AsyncRelayCommand(TestSpeakAsync);
        LoadVoicesCommand = new AsyncRelayCommand(LoadVoicesAsync);

        foreach (var d in deviceEnumerator.GetOutputDevices())
            OutputDevices.Add(d);

        foreach (var engineId in engineRegistry.GetEnabledEngineIds())
            AvailableEngines.Add(new EngineOption(engineId, GetEngineDisplayName(engineId)));

        SpeakVoices.Add(RandomVoiceSentinel);

        Refresh();
        _ = LoadVoicesAsync();
    }

    public void Refresh()
    {
        Aliases.Clear();
        foreach (var a in _repo.GetAllSorted())
            Aliases.Add(a);
    }

    private void LoadAliasDetail()
    {
        if (_selectedAlias == null) return;
        DetailName = _selectedAlias.Name;
        _detailEngineId = _selectedAlias.EngineId;
        OnPropertyChanged(nameof(DetailEngineId));
        var deviceId = _selectedAlias.OutputDeviceId ?? string.Empty;
        if (OutputDevices.Count > 0 && !OutputDevices.Any(d => d.Id == deviceId))
        {
            deviceId = OutputDevices[0].Id;
            _selectedAlias.OutputDeviceId = deviceId;
            _repo.Upsert(_selectedAlias);
        }
        _detailOutputDeviceId = deviceId;
        OnPropertyChanged(nameof(DetailOutputDeviceId));
        TestVolume = _selectedAlias.Volume;
        SelectedVoice = AvailableVoices.FirstOrDefault(v => v.Id == _selectedAlias.VoiceId);

        AliasVoices.Clear();
        foreach (var voiceId in _selectedAlias.VoiceIds)
        {
            var voice = AvailableVoices.FirstOrDefault(v => v.Id == voiceId);
            if (voice != null) AliasVoices.Add(voice);
        }

        InUseTree.Clear();
        InUseUsers.Clear();
        var users = _userRepo.GetAll().Where(u => u.AliasName == _selectedAlias.Name).ToList();
        foreach (var u in users)
            InUseUsers.Add(u.Username);
        if (users.Count > 0)
        {
            var node = new AliasUserNode { Label = "Viewers", Children = users.Select(u => new AliasUserNode { Label = u.Username }).ToList() };
            InUseTree.Add(node);
        }

        RebuildParamRows();
    }

    private void RebuildParamRows()
    {
        var engine = _engineRegistry.GetEngine(_detailEngineId);
        var schema = engine?.GetParameters() ?? Array.Empty<EngineParameterDef>();
        var saved = SynthParams.FromJson(_selectedAlias?.EngineParamsJson);

        ParamRows.Clear();
        foreach (var def in schema)
            ParamRows.Add(new AliasParamRow { Def = def, Value = saved.Str(def.Key, def.Default) });
    }

    private static string GetEngineDisplayName(string engineId) => engineId switch
    {
        EngineIds.Sapi5       => "SAPI5",
        EngineIds.Azure       => "Azure",
        EngineIds.AmazonPolly => "Amazon Polly",
        EngineIds.GoogleCloud => "Google Cloud",
        EngineIds.ElevenLabs  => "ElevenLabs",
        EngineIds.TtsMonster  => "TTSMonster",
        EngineIds.IbmWatson   => "IBM Watson",
        EngineIds.Acapela     => "Acapela",
        EngineIds.CereProc    => "CereProc",
        EngineIds.UberDuck    => "Uberduck",
        _                     => engineId
    };

    private async Task LoadVoicesAsync()
    {
        var allVoices = new List<VoiceInfo>();
        foreach (var engineId in _engineRegistry.GetEnabledEngineIds())
        {
            var engine = _engineRegistry.GetEngine(engineId);
            if (engine == null) continue;
            try
            {
                var voices = await engine.GetVoicesAsync();
                allVoices.AddRange(voices.Select(v => new VoiceInfo { Id = v.Id, Name = v.Name, Locale = v.Locale, Gender = v.Gender, EngineId = engineId }));
                _logger?.Info($"TTS :: Added {GetEngineDisplayName(engineId)} with {voices.Count} voices");
            }
            catch { }
        }
        _logger?.Info("TTS :: Text To Speech Service initialized!");
        Application.Current?.Dispatcher.Invoke(() =>
        {
            AvailableVoices.Clear();
            SpeakVoices.Clear();
            SpeakVoices.Add(RandomVoiceSentinel);
            foreach (var v in allVoices)
            {
                AvailableVoices.Add(v);
                SpeakVoices.Add(v);
            }
            TestSpeakVoice = RandomVoiceSentinel;
            if (_selectedAlias != null) LoadAliasDetail();
        });
    }

    private void AddAlias()
    {
        if (string.IsNullOrWhiteSpace(DetailName)) return;
        var alias = new VoiceAlias { Name = DetailName };
        _repo.Upsert(alias);
        Refresh();
        SelectedAlias = Aliases.FirstOrDefault(a => a.Name == DetailName);
    }

    private void DeleteAlias()
    {
        if (_selectedAlias == null) return;
        if (MessageBox.Show($"Delete alias '{_selectedAlias.Name}'?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            _repo.Delete(_selectedAlias.Id);
            Refresh();
            SelectedAlias = null;
        }
    }

    private void Rename()
    {
        if (_selectedAlias == null || string.IsNullOrWhiteSpace(DetailName)) return;
        _selectedAlias.Name = DetailName;
        _repo.Upsert(_selectedAlias);
        Refresh();
        SelectedAlias = Aliases.FirstOrDefault(a => a.Name == DetailName);
    }

    private void SaveAliasDetail()
    {
        if (_selectedAlias == null) return;
        _selectedAlias.Name = DetailName;
        _selectedAlias.EngineId = DetailEngineId;
        _selectedAlias.VoiceId = SelectedVoice?.Id ?? _selectedAlias.VoiceId;
        _selectedAlias.Volume = TestVolume;
        _selectedAlias.OutputDeviceId = DetailOutputDeviceId;
        _selectedAlias.EngineParamsJson = SerializeParamRows();
        _repo.Upsert(_selectedAlias);
        var savedName = _selectedAlias.Name;
        Refresh();
        SelectedAlias = Aliases.FirstOrDefault(a => a.Name == savedName);
    }

    private string SerializeParamRows()
    {
        if (ParamRows.Count == 0) return "{}";
        var dict = ParamRows.ToDictionary(r => r.Def.Key, r => r.Value);
        return JsonSerializer.Serialize(dict);
    }

    private void AddVoice()
    {
        if (_selectedAlias == null || TestSpeakVoice?.Id is not { Length: > 0 } voiceId) return;
        if (!_selectedAlias.VoiceIds.Contains(voiceId))
        {
            _selectedAlias.VoiceIds.Add(voiceId);
            _repo.Upsert(_selectedAlias);
            AliasVoices.Add(TestSpeakVoice);
        }
    }

    private void RemoveVoice()
    {
        if (_selectedAlias == null || _selectedVoice == null) return;
        _selectedAlias.VoiceIds.Remove(_selectedVoice.Id);
        if (_selectedAlias.VoiceId == _selectedVoice.Id)
            _selectedAlias.VoiceId = _selectedAlias.VoiceIds.FirstOrDefault() ?? string.Empty;
        _repo.Upsert(_selectedAlias);
        AliasVoices.Remove(_selectedVoice);
        SelectedVoice = null;
    }

    private void RemoveAllVoices()
    {
        if (_selectedAlias == null) return;
        _selectedAlias.VoiceIds.Clear();
        _selectedAlias.VoiceId = string.Empty;
        _repo.Upsert(_selectedAlias);
        AliasVoices.Clear();
        SelectedVoice = null;
    }

    private void SetDefaultVoice()
    {
        if (_selectedAlias == null || TestSpeakVoice?.Id is not { Length: > 0 } voiceId) return;
        _selectedAlias.VoiceId = voiceId;
        if (TestSpeakVoice.EngineId is { Length: > 0 } eid && eid != _selectedAlias.EngineId)
        {
            _selectedAlias.EngineId = eid;
            DetailEngineId = eid;
        }
        if (!_selectedAlias.VoiceIds.Contains(voiceId))
        {
            _selectedAlias.VoiceIds.Add(voiceId);
            AliasVoices.Add(TestSpeakVoice);
        }
        _repo.Upsert(_selectedAlias);
    }

    private async Task TestSpeakAsync()
    {
        var alias = _selectedAlias;
        if (alias == null) return;
        var selectedVoice = TestSpeakVoice;
        var engineId = selectedVoice?.EngineId is { Length: > 0 } eid ? eid : alias.EngineId;
        var engine = _engineRegistry.GetEngine(engineId) ?? _engineRegistry.GetDefaultEngine();
        var voiceId = selectedVoice?.Id is { Length: > 0 } vid ? vid : alias.VoiceId;
        var paramDict = ParamRows.ToDictionary(r => r.Def.Key, r => r.Value);
        var synthParams = new SynthParams(paramDict);
        var audio = await engine.SynthesizeAsync(TestMessage, voiceId, synthParams);
        if (!audio.IsEmpty)
        {
            var player = new Audio.NAudioPlayer();
            await player.PlayAsync(audio, alias.OutputDeviceId, TestVolume);
        }
    }
}
