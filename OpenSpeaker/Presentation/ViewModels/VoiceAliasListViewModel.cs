using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using OpenSpeaker.Audio;
using OpenSpeaker.Data;
using OpenSpeaker.Infrastructure.Logging;
using OpenSpeaker.Models;
using OpenSpeaker.Services;
using OpenSpeaker.TTS;
namespace OpenSpeaker.ViewModels;

public class AliasUserNode
{
    public string Label { get; init; } = string.Empty;
    public List<AliasUserNode> Children { get; init; } = new();
}

public class VoiceAliasListViewModel : BaseViewModel, IDisposable
{
    private readonly VoiceAliasRepository _repo;
    private readonly TtsEngineRegistry _engineRegistry;
    private readonly VoicePool _voicePool;
    private readonly AudioDeviceEnumerator _deviceEnumerator;
    private readonly UserRepository _userRepo;
    private readonly Func<IReadOnlyList<UserRecord>> _getAllUsers;
    private readonly IAppLogger? _logger;

    public static readonly VoiceInfo RandomVoiceSentinel = new() { Id = string.Empty, Name = "Random Voice" };
    public event EventHandler? VoicesLoaded;

    private List<VoiceInfo> _allSpeakVoices = new();
    private List<VoiceAlias> _allAliases = new();

    private string _aliasFilter = string.Empty;
    public string AliasFilter
    {
        get => _aliasFilter;
        set { SetField(ref _aliasFilter, value); ApplyAliasFilter(); }
    }

    private string _voiceFilter = "";
    public string VoiceFilter
    {
        get => _voiceFilter;
        set
        {
            SetField(ref _voiceFilter, value);
            SpeakVoices = new ObservableCollection<VoiceInfo>(
                string.IsNullOrEmpty(value) ? _allSpeakVoices
                : _allSpeakVoices.Where(v => string.IsNullOrEmpty(v.Id) || v.DisplayName.Contains(value, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public ObservableCollection<VoiceAlias> Aliases { get; } = new();
    public ObservableCollection<AudioDeviceInfo> OutputDevices { get; } = new();
    private ObservableCollection<VoiceInfo> _availableVoices = new();
    public ObservableCollection<VoiceInfo> AvailableVoices { get => _availableVoices; private set { _availableVoices = value; OnPropertyChanged(); } }
    private ObservableCollection<VoiceInfo> _speakVoices = new();
    public ObservableCollection<VoiceInfo> SpeakVoices { get => _speakVoices; private set { _speakVoices = value; OnPropertyChanged(); } }
    public ObservableCollection<EngineOption> AvailableEngines { get; } = new();
    public ObservableCollection<AliasUserNode> InUseTree { get; } = new();
    public ObservableCollection<string> InUseUsers { get; } = new();
    public ObservableCollection<VoiceInfo> AliasVoices { get; } = new();
    public ObservableCollection<AliasParamRow> ParamRows { get; } = new();

    private VoiceAlias? _selectedAlias;
    public VoiceAlias? SelectedAlias
    {
        get => _selectedAlias;
        set { SetField(ref _selectedAlias, value); OnPropertyChanged(nameof(HasSelected)); VoiceFilter = ""; LoadAliasDetail(); }
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
    public int TestVolume
    {
        get => _testVolume;
        set
        {
            if (!SetField(ref _testVolume, value)) return;
            if (_selectedAlias != null && _selectedAlias.Volume != value)
            {
                _selectedAlias.Volume = value;
                _repo.Upsert(_selectedAlias);
            }
        }
    }

    private string _newAliasName = string.Empty;
    public string NewAliasName { get => _newAliasName; set => SetField(ref _newAliasName, value); }

    private VoiceInfo? _testSpeakVoice = RandomVoiceSentinel;
    public VoiceInfo? TestSpeakVoice
    {
        get => _testSpeakVoice;
        set
        {
            if (!SetField(ref _testSpeakVoice, value)) return;
            if (value != null && !string.IsNullOrEmpty(_voiceFilter))
                VoiceFilter = "";
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
                if (_selectedAlias != null && _selectedVoice != null)
                {
                    var idx = AliasVoices.IndexOf(_selectedVoice);
                    if (idx >= 0)
                    {
                        var replacedId = _selectedVoice.Id;
                        AliasVoices[idx] = value;
                        var idxInPool = _selectedAlias.VoiceIds.IndexOf(replacedId);
                        if (idxInPool >= 0) _selectedAlias.VoiceIds[idxInPool] = value.Id;
                        if (_selectedAlias.VoiceId == replacedId)
                        {
                            _selectedAlias.VoiceId = value.Id;
                            _selectedAlias.EngineId = _detailEngineId;
                        }
                        _selectedVoice = value;
                        OnPropertyChanged(nameof(SelectedVoice));
                        _repo.Upsert(_selectedAlias);
                    }
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
            if (_selectedAlias != null)
            {
                _selectedAlias.VoiceId = value.Id;
                _selectedAlias.EngineId = _detailEngineId;
                _repo.Upsert(_selectedAlias);
            }
        }
    }

    private List<UserRecord> _allDbUsers = new();

    private string _userFilter = string.Empty;
    public string UserFilter
    {
        get => _userFilter;
        set { SetField(ref _userFilter, value); ApplyUserFilter(); }
    }

    private List<UserRecord> _filteredUsers = new();
    public List<UserRecord> FilteredUsers
    {
        get => _filteredUsers;
        private set { _filteredUsers = value; OnPropertyChanged(); }
    }

    private UserRecord? _selectedUserToAdd;
    public UserRecord? SelectedUserToAdd
    {
        get => _selectedUserToAdd;
        set { SetField(ref _selectedUserToAdd, value); }
    }

    public RelayCommand AddAliasCommand { get; }
    public RelayCommand DeleteAliasCommand { get; }
    public RelayCommand RenameCommand { get; }
    public RelayCommand SaveAliasDetailCommand { get; }
    public RelayCommand AddVoiceCommand { get; }
    public RelayCommand RemoveVoiceCommand { get; }
    public RelayCommand RemoveAllVoicesCommand { get; }
    public RelayCommand SetDefaultVoiceCommand { get; }
    public RelayCommand AddUserToAliasCommand { get; }
    public RelayCommand RemoveUserFromAliasCommand { get; }
    public AsyncRelayCommand TestSpeakCommand { get; }
    public AsyncRelayCommand LoadVoicesCommand { get; }

    private readonly IDialogService _dialogs;

    public VoiceAliasListViewModel(VoiceAliasRepository repo, TtsEngineRegistry engineRegistry, VoicePool voicePool, AudioDeviceEnumerator deviceEnumerator, UserRepository userRepo, Func<IReadOnlyList<UserRecord>> getAllUsers, IAppLogger? logger = null, IDialogService? dialogs = null)
    {
        _repo = repo;
        _engineRegistry = engineRegistry;
        _getAllUsers = getAllUsers;
        _voicePool = voicePool;
        _deviceEnumerator = deviceEnumerator;
        _userRepo = userRepo;
        _logger = logger;
        _dialogs = dialogs ?? new DialogService();

        AddAliasCommand = new RelayCommand(AddAlias, () => !string.IsNullOrWhiteSpace(DetailName));
        DeleteAliasCommand = new RelayCommand(DeleteAlias, () => SelectedAlias != null);
        RenameCommand = new RelayCommand(Rename, () => SelectedAlias != null && !string.IsNullOrWhiteSpace(DetailName));
        SaveAliasDetailCommand = new RelayCommand(SaveAliasDetail, () => SelectedAlias != null);
        AddVoiceCommand = new RelayCommand(AddVoice, () => SelectedAlias != null && TestSpeakVoice?.Id is { Length: > 0 });
        RemoveVoiceCommand = new RelayCommand(RemoveVoice, () => SelectedAlias != null && _selectedVoice != null);
        RemoveAllVoicesCommand = new RelayCommand(RemoveAllVoices, () => SelectedAlias != null && AliasVoices.Count > 0);
        SetDefaultVoiceCommand = new RelayCommand(SetDefaultVoice, () => SelectedAlias != null && TestSpeakVoice?.Id is { Length: > 0 });
        AddUserToAliasCommand = new RelayCommand(AddUserToAlias, () => _selectedAlias != null && SelectedUserToAdd != null);
        RemoveUserFromAliasCommand = new RelayCommand(RemoveUserFromAlias);
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
        _allAliases = _repo.GetAllSorted().ToList();
        ApplyAliasFilter();
    }

    private void ApplyAliasFilter()
    {
        var filtered = string.IsNullOrEmpty(_aliasFilter)
            ? _allAliases
            : _allAliases.Where(a => (a.Name ?? "").Contains(_aliasFilter, StringComparison.OrdinalIgnoreCase));
        Aliases.Clear();
        foreach (var a in filtered)
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

        VoiceInfo? FindVoice(string? id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var byId   = AvailableVoices.FirstOrDefault(v => v.Id == id);
            if (byId != null) return byId;
            var byName = AvailableVoices.FirstOrDefault(v => string.Equals(v.Name, id, StringComparison.OrdinalIgnoreCase));
            if (byName != null) return byName;
            if (!id.Contains('(')) return null;
            var stripped = id[..id.LastIndexOf('(')].Trim();
            return AvailableVoices.FirstOrDefault(v => string.Equals(v.Name, stripped, StringComparison.OrdinalIgnoreCase));
        }

        var needsSave = false;
        SelectedVoice = FindVoice(_selectedAlias.VoiceId);
        if (SelectedVoice != null && SelectedVoice.Id != _selectedAlias.VoiceId)
        {
            _selectedAlias.VoiceId = SelectedVoice.Id;
            needsSave = true;
        }

        AliasVoices.Clear();
        var resolvedIds = _selectedAlias.VoiceIds.ToList();
        for (var i = 0; i < resolvedIds.Count; i++)
        {
            var voice = FindVoice(resolvedIds[i]);
            if (voice == null) continue;
            AliasVoices.Add(voice);
            if (voice.Id != resolvedIds[i])
            {
                resolvedIds[i] = voice.Id;
                needsSave = true;
            }
        }
        if (needsSave)
        {
            _selectedAlias.VoiceIds = resolvedIds;
            _repo.Upsert(_selectedAlias);
        }

        InUseTree.Clear();
        InUseUsers.Clear();
        _allDbUsers = _getAllUsers().OrderBy(u => u.Username).ToList();
        var users = _allDbUsers.Where(u => u.AliasName == _selectedAlias.Name).ToList();
        foreach (var u in users)
            InUseUsers.Add(u.Username);
        if (users.Count > 0)
        {
            var node = new AliasUserNode { Label = "Viewers", Children = users.Select(u => new AliasUserNode { Label = u.Username }).ToList() };
            InUseTree.Add(node);
        }
        UserFilter = string.Empty;
        ApplyUserFilter();

        RebuildParamRows();
    }

    private void RebuildParamRows()
    {
        foreach (var row in ParamRows)
            row.PropertyChanged -= OnParamChanged;

        var engine = _engineRegistry.GetEngine(_detailEngineId);
        var schema = engine?.GetParameters() ?? Array.Empty<EngineParameterDef>();
        var saved = SynthParams.FromJson(_selectedAlias?.EngineParamsJson);

        ParamRows.Clear();
        foreach (var def in schema)
        {
            var value = saved.Str(def.Key, def.Default);
            if (def.Type == EngineParameterType.ComboBox && def.Options != null && !def.Options.Contains(value))
                value = def.Default;
            var row = new AliasParamRow { Def = def, Value = value };
            if (def.Type == EngineParameterType.SearchableVoice && engine is IVoiceSearchEngine search)
                row.AttachSearch(search);
            row.PropertyChanged += OnParamChanged;
            ParamRows.Add(row);
        }
    }

    private void OnParamChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AliasParamRow.Value) || _selectedAlias == null) return;
        _selectedAlias.EngineParamsJson = SerializeParamRows();
        _repo.Upsert(_selectedAlias);
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
        var all = await _voicePool.GetAllAsync();
        var allVoices = all.Select(p =>
        {
            var locale = p.Voice.Locale ?? string.Empty;
            var name = p.Voice.Name;
            if (!string.IsNullOrEmpty(locale) && !name.Contains(locale, StringComparison.OrdinalIgnoreCase))
                name = $"{name} ({locale})";
            return new VoiceInfo { Id = p.Voice.Id, Name = name, Locale = locale, Gender = p.Voice.Gender, EngineId = p.EngineId, DisplayName = $"{_engineRegistry.GetDisplayName(p.EngineId)}: {name}" };
        }).ToList();

        foreach (var grp in all.GroupBy(p => p.EngineId))
        {
            var displayName = _engineRegistry.GetDisplayName(grp.Key);
            if (!grp.Any()) _logger?.Warn($"TTS :: {displayName} loaded 0 voices, check auth/config");
            else _logger?.Info($"TTS :: Added {displayName} with {grp.Count()} voices");
        }
        _logger?.Info("TTS :: Text To Speech Service initialized!");

        Application.Current?.Dispatcher.Invoke(() =>
        {
            _allSpeakVoices = allVoices.Prepend(RandomVoiceSentinel).ToList();
            AvailableVoices = new ObservableCollection<VoiceInfo>(allVoices);
            SpeakVoices     = new ObservableCollection<VoiceInfo>(_allSpeakVoices);
            TestSpeakVoice  = RandomVoiceSentinel;
            if (_selectedAlias != null) LoadAliasDetail();
            VoicesLoaded?.Invoke(this, EventArgs.Empty);
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
        if (_dialogs.Confirm($"Delete alias '{_selectedAlias.Name}'?", "Confirm"))
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
            if (string.IsNullOrEmpty(_selectedAlias.VoiceId))
            {
                _selectedAlias.VoiceId = voiceId;
                if (TestSpeakVoice.EngineId is { Length: > 0 } eid)
                {
                    _selectedAlias.EngineId = eid;
                    DetailEngineId = eid;
                }
            }
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

    private DispatcherTimer? _userRefreshTimer;

    public void NotifyUserActivity(string twitchId)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (_userRefreshTimer != null) return;
            if (_allDbUsers.Any(u => u.TwitchId == twitchId)) return;

            _userRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _userRefreshTimer.Tick += (_, _) =>
            {
                _userRefreshTimer?.Stop();
                _userRefreshTimer = null;
                RefreshAvailableUsers();
            };
            _userRefreshTimer.Start();
        });
    }

    private void RefreshAvailableUsers()
    {
        _allDbUsers = _userRepo.GetAll().OrderBy(u => u.Username).ToList();
        ApplyUserFilter();
    }

    public void Dispose()
    {
        _userRefreshTimer?.Stop();
        _userRefreshTimer = null;
    }

    private void ApplyUserFilter()
    {
        if (_selectedAlias == null) return;
        var assigned = new HashSet<string>(InUseUsers, StringComparer.OrdinalIgnoreCase);
        FilteredUsers = _allDbUsers
            .Where(u => !string.IsNullOrEmpty(u.Username))
            .Where(u => !assigned.Contains(u.Username))
            .Where(u => string.IsNullOrEmpty(_userFilter) ||
                        u.Username.Contains(_userFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private void AddUserToAlias()
    {
        if (_selectedAlias == null || SelectedUserToAdd == null) return;
        SelectedUserToAdd.AliasName = _selectedAlias.Name;
        _userRepo.Upsert(SelectedUserToAdd);
        InUseUsers.Add(SelectedUserToAdd.Username);
        SelectedUserToAdd = null;
        ApplyUserFilter();
    }

    private void RemoveUserFromAlias(object? param)
    {
        if (_selectedAlias == null || param is not string username) return;
        var user = _allDbUsers.FirstOrDefault(u => u.Username == username);
        if (user == null) return;
        user.AliasName = string.Empty;
        _userRepo.Upsert(user);
        InUseUsers.Remove(username);
        ApplyUserFilter();
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
            audio = Audio.AudioGain.Apply(audio, TestVolume);
            var player = new Audio.NAudioPlayer();
            await player.PlayAsync(audio, alias.OutputDeviceId, 100);
        }
    }
}
