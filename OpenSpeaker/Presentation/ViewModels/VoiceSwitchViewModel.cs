using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OpenSpeaker.Data;
using OpenSpeaker.Models;
using OpenSpeaker.Text;
namespace OpenSpeaker.ViewModels;

public class VoiceSwitchScope : INotifyPropertyChanged
{
	public string Username { get; init; } = string.Empty;
	public bool IsGlobal => string.IsNullOrEmpty(Username);
	public bool IsUnknownUser { get; init; }

	private int _count;
	public int Count { get => _count; set { _count = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSwitches)); } }
	public bool HasSwitches => _count > 0;

	public event PropertyChangedEventHandler? PropertyChanged;
	private void OnPropertyChanged([CallerMemberName] string? name = null)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class VoiceSwitchViewModel : BaseViewModel
{
	private readonly VoiceSwitchRepository _repo;
	private readonly VoiceAliasRepository _aliasRepo;
	private readonly Func<IReadOnlyList<UserRecord>> _getUsers;
	private readonly VoiceSwitchScope _globalScope = new();
	private List<VoiceSwitch> _all = new();
	private int _knownUserCount = -1;
	private bool _suppressItemChanges;

	public ObservableCollection<VoiceSwitchScope> Scopes { get; } = new();
	public ObservableCollection<VoiceSwitch> Switches { get; } = new();
	public ObservableCollection<string> AliasNames { get; } = new();

	private List<string> _filteredAliasNames = new();
	public List<string> FilteredAliasNames
	{
		get => _filteredAliasNames;
		private set { _filteredAliasNames = value; OnPropertyChanged(); }
	}

	private string _aliasFilter = string.Empty;
	public string AliasFilter
	{
		get => _aliasFilter;
		set { SetField(ref _aliasFilter, value); ApplyAliasFilter(); }
	}

	private string _scopeFilter = string.Empty;
	public string ScopeFilter
	{
		get => _scopeFilter;
		set { SetField(ref _scopeFilter, value); RebuildScopes(); }
	}

	private VoiceSwitchScope? _selectedScope;
	public VoiceSwitchScope? SelectedScope
	{
		get => _selectedScope;
		set
		{
			if (value == null && _selectedScope != null) return;
			SetField(ref _selectedScope, value);
			OnPropertyChanged(nameof(HasSelectedScope));
			LoadScopeSwitches();
			ApplySample();
		}
	}

	public bool HasSelectedScope => _selectedScope != null;

	private VoiceSwitch? _selectedSwitch;
	public VoiceSwitch? SelectedSwitch
	{
		get => _selectedSwitch;
		set { SetField(ref _selectedSwitch, value); LoadEditor(); CommandManager.InvalidateRequerySuggested(); }
	}

	private string _editMarker = string.Empty;
	public string EditMarker { get => _editMarker; set => SetField(ref _editMarker, value); }

	private string? _editAliasName;
	public string? EditAliasName { get => _editAliasName; set => SetField(ref _editAliasName, value); }

	private bool _editEnabled = true;
	public bool EditEnabled { get => _editEnabled; set => SetField(ref _editEnabled, value); }

	private string _sampleInput = string.Empty;
	public string SampleInput { get => _sampleInput; set { SetField(ref _sampleInput, value); ApplySample(); } }

	public ObservableCollection<VoiceSwitchSegment> SampleSegments { get; } = new();

	public RelayCommand AddSwitchCommand { get; }
	public RelayCommand DeleteSwitchCommand { get; }
	public RelayCommand SaveSwitchCommand { get; }

	public VoiceSwitchViewModel(VoiceSwitchRepository repo, VoiceAliasRepository aliasRepo, Func<IReadOnlyList<UserRecord>> getUsers)
	{
		_repo = repo;
		_aliasRepo = aliasRepo;
		_getUsers = getUsers;

		AddSwitchCommand    = new RelayCommand(AddSwitch,    () => SelectedScope != null && CanCommitEditor());
		DeleteSwitchCommand = new RelayCommand(DeleteSwitch, () => SelectedSwitch != null);
		SaveSwitchCommand   = new RelayCommand(SaveSwitch,   () => SelectedSwitch != null && CanCommitEditor());

		RefreshAliases();
		Refresh();
		SelectedScope = _globalScope;
	}

	private bool CanCommitEditor() =>
		!string.IsNullOrWhiteSpace(EditMarker) && !string.IsNullOrWhiteSpace(EditAliasName);

	private static bool SameUser(string a, string b) =>
		string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

	public void RefreshAliases()
	{
		var current = EditAliasName;
		AliasNames.Clear();
		foreach (var a in _aliasRepo.GetAllSorted())
			AliasNames.Add(a.Name);
		ApplyAliasFilter();
		EditAliasName = current;
	}

	private void ApplyAliasFilter()
	{
		FilteredAliasNames = string.IsNullOrEmpty(_aliasFilter)
			? AliasNames.ToList()
			: AliasNames.Where(n => n.Contains(_aliasFilter, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(n, _editAliasName, StringComparison.Ordinal)).ToList();
	}

	public void Refresh()
	{
		_all = _repo.GetAll().ToList();
		RebuildScopes();
		LoadScopeSwitches();
		ApplySample();
	}

	public void SyncUsers()
	{
		if (_getUsers().Count == _knownUserCount) return;
		RebuildScopes();
	}

	private void RebuildScopes()
	{
		var users = _getUsers();
		_knownUserCount = users.Count;

		var names = users
			.Select(u => u.Username)
			.Concat(_all.Where(s => !s.IsGlobal).Select(s => s.Username.Trim()))
			.Where(n => !string.IsNullOrWhiteSpace(n))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		var filter = _scopeFilter.Trim().TrimStart('@');
		var matches = string.IsNullOrEmpty(filter)
			? names
			: names.Where(n => n.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

		var scopes = new List<VoiceSwitchScope> { _globalScope };
		if (!string.IsNullOrEmpty(filter) && !names.Any(n => SameUser(n, filter)))
			scopes.Add(new VoiceSwitchScope { Username = filter, IsUnknownUser = true });
		scopes.AddRange(matches
			.Select(n => new VoiceSwitchScope { Username = n, Count = CountFor(n) })
			.OrderByDescending(s => s.HasSwitches)
			.ThenBy(s => s.Username, StringComparer.OrdinalIgnoreCase));
		_globalScope.Count = _all.Count(s => s.IsGlobal);

		var selectedName = _selectedScope?.Username;
		Scopes.Clear();
		foreach (var s in scopes) Scopes.Add(s);

		var reselect = selectedName == null ? null : Scopes.FirstOrDefault(s => SameUser(s.Username, selectedName));
		if (reselect != null) _selectedScope = reselect;
		OnPropertyChanged(nameof(SelectedScope));
	}

	private int CountFor(string username) =>
		_all.Count(s => !s.IsGlobal && SameUser(s.Username, username));

	private void UpdateScopeCounts()
	{
		foreach (var scope in Scopes)
			scope.Count = scope.IsGlobal ? _all.Count(s => s.IsGlobal) : CountFor(scope.Username);
	}

	private IEnumerable<VoiceSwitch> SwitchesFor(VoiceSwitchScope scope) =>
		scope.IsGlobal
			? _all.Where(s => s.IsGlobal)
			: _all.Where(s => !s.IsGlobal && SameUser(s.Username, scope.Username));

	private void LoadScopeSwitches()
	{
		foreach (var s in Switches) s.PropertyChanged -= OnItemPropertyChanged;
		Switches.Clear();
		SelectedSwitch = null;
		if (_selectedScope == null) return;
		foreach (var s in SwitchesFor(_selectedScope).OrderBy(s => s.Marker, StringComparer.OrdinalIgnoreCase))
		{
			s.PropertyChanged += OnItemPropertyChanged;
			Switches.Add(s);
		}
	}

	private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (_suppressItemChanges || sender is not VoiceSwitch s || e.PropertyName == nameof(VoiceSwitch.IsGlobal)) return;
		_repo.Upsert(s);
		if (s == _selectedSwitch) LoadEditor();
		ApplySample();
	}

	private void LoadEditor()
	{
		var s = _selectedSwitch;
		EditMarker    = s?.Marker ?? string.Empty;
		EditAliasName = s?.AliasName;
		EditEnabled   = s?.Enabled ?? true;
	}

	private void AddSwitch()
	{
		if (_selectedScope == null) return;
		var s = new VoiceSwitch
		{
			Marker    = EditMarker.Trim(),
			AliasName = EditAliasName ?? string.Empty,
			Username  = _selectedScope.Username,
			Enabled   = EditEnabled,
		};
		_repo.Insert(s);
		_all.Add(s);

		if (_selectedScope.IsUnknownUser)
		{
			RebuildScopes();
			LoadScopeSwitches();
		}
		else
		{
			s.PropertyChanged += OnItemPropertyChanged;
			Switches.Add(s);
			UpdateScopeCounts();
		}
		SelectedSwitch = Switches.FirstOrDefault(x => x == s);
		ApplySample();
	}

	private void DeleteSwitch()
	{
		if (_selectedSwitch == null) return;
		var s = _selectedSwitch;
		_repo.Delete(s.Id);
		_all.Remove(s);
		s.PropertyChanged -= OnItemPropertyChanged;
		Switches.Remove(s);
		SelectedSwitch = null;
		UpdateScopeCounts();
		ApplySample();
	}

	private void SaveSwitch()
	{
		if (_selectedSwitch == null) return;
		_suppressItemChanges = true;
		_selectedSwitch.Marker    = EditMarker.Trim();
		_selectedSwitch.AliasName = EditAliasName ?? string.Empty;
		_selectedSwitch.Enabled   = EditEnabled;
		_suppressItemChanges = false;
		_repo.Upsert(_selectedSwitch);
		ApplySample();
	}

	private void ApplySample()
	{
		SampleSegments.Clear();
		if (string.IsNullOrWhiteSpace(_sampleInput)) return;
		foreach (var segment in VoiceSwitchParser.Split(_sampleInput, _selectedScope?.Username, _all))
			SampleSegments.Add(segment);
	}
}
