using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OpenSpeaker.Audio;
using OpenSpeaker.Data;
using OpenSpeaker.Models;
using OpenSpeaker.Queue;
using OpenSpeaker.Text;
namespace OpenSpeaker.ViewModels;

public class WordDictionaryScope : INotifyPropertyChanged
{
	public string AliasName { get; init; } = string.Empty;
	public bool IsGlobal => string.IsNullOrEmpty(AliasName);

	private int _count;
	public int Count { get => _count; set { _count = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasEntries)); } }
	public bool HasEntries => _count > 0;

	public event PropertyChangedEventHandler? PropertyChanged;
	private void OnPropertyChanged([CallerMemberName] string? name = null)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class WordDictionaryViewModel : BaseViewModel
{
	private readonly WordDictionaryRepository _repo;
	private readonly VoiceAliasRepository _aliasRepo;
	private readonly VoiceResolver _resolver;
	private readonly SettingsRepository _settingsRepo;
	private readonly WordDictionaryScope _globalScope = new();
	private List<WordDictionaryEntry> _all = new();
	private bool _suppressItemChanges;

	public ObservableCollection<WordDictionaryScope> Scopes { get; } = new();
	public ObservableCollection<WordDictionaryEntry> Entries { get; } = new();

	private string _scopeFilter = string.Empty;
	public string ScopeFilter
	{
		get => _scopeFilter;
		set { SetField(ref _scopeFilter, value); RebuildScopes(); }
	}

	private WordDictionaryScope? _selectedScope;
	public WordDictionaryScope? SelectedScope
	{
		get => _selectedScope;
		set
		{
			if (value == null && _selectedScope != null) return;
			SetField(ref _selectedScope, value);
			OnPropertyChanged(nameof(HasSelectedScope));
			LoadScopeEntries();
			ApplySample();
		}
	}

	public bool HasSelectedScope => _selectedScope != null;

	private WordDictionaryEntry? _selectedEntry;
	public WordDictionaryEntry? SelectedEntry
	{
		get => _selectedEntry;
		set { SetField(ref _selectedEntry, value); LoadEditor(); CommandManager.InvalidateRequerySuggested(); }
	}

	private string _editWord = string.Empty;
	public string EditWord { get => _editWord; set => SetField(ref _editWord, value); }

	private string _editReplacement = string.Empty;
	public string EditReplacement { get => _editReplacement; set => SetField(ref _editReplacement, value); }

	private bool _editEnabled = true;
	public bool EditEnabled { get => _editEnabled; set => SetField(ref _editEnabled, value); }

	private string _sampleInput = string.Empty;
	public string SampleInput { get => _sampleInput; set { SetField(ref _sampleInput, value); ApplySample(); } }

	private string _sampleOutput = string.Empty;
	public string SampleOutput { get => _sampleOutput; private set => SetField(ref _sampleOutput, value); }

	public RelayCommand AddEntryCommand { get; }
	public RelayCommand DeleteEntryCommand { get; }
	public RelayCommand SaveEntryCommand { get; }
	public AsyncRelayCommand PlaySampleCommand { get; }

	public WordDictionaryViewModel(WordDictionaryRepository repo, VoiceAliasRepository aliasRepo, VoiceResolver resolver, SettingsRepository settingsRepo)
	{
		_repo = repo;
		_aliasRepo = aliasRepo;
		_resolver = resolver;
		_settingsRepo = settingsRepo;

		AddEntryCommand    = new RelayCommand(AddEntry,    () => SelectedScope != null && CanCommitEditor());
		DeleteEntryCommand = new RelayCommand(DeleteEntry, () => SelectedEntry != null);
		SaveEntryCommand   = new RelayCommand(SaveEntry,   () => SelectedEntry != null && CanCommitEditor());
		PlaySampleCommand  = new AsyncRelayCommand(PlaySampleAsync, () => !string.IsNullOrWhiteSpace(SampleOutput));

		Refresh();
		SelectedScope = _globalScope;
	}

	private bool CanCommitEditor() =>
		!string.IsNullOrWhiteSpace(EditWord) && !string.IsNullOrWhiteSpace(EditReplacement);

	private static bool SameAlias(string a, string b) =>
		string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

	public void Refresh()
	{
		_all = _repo.GetAll().ToList();
		RebuildScopes();
		LoadScopeEntries();
		ApplySample();
	}

	public void RefreshAliases() => RebuildScopes();

	private void RebuildScopes()
	{
		var names = _aliasRepo.GetAllSorted().Select(a => a.Name)
			.Concat(_all.Where(e => !e.IsGlobal).Select(e => e.AliasName.Trim()))
			.Where(n => !string.IsNullOrWhiteSpace(n))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		var filter = _scopeFilter.Trim();
		var matches = string.IsNullOrEmpty(filter)
			? names
			: names.Where(n => n.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

		var scopes = new List<WordDictionaryScope> { _globalScope };
		scopes.AddRange(matches
			.Select(n => new WordDictionaryScope { AliasName = n, Count = CountFor(n) })
			.OrderByDescending(s => s.HasEntries)
			.ThenBy(s => s.AliasName, StringComparer.OrdinalIgnoreCase));
		_globalScope.Count = _all.Count(e => e.IsGlobal);

		var selectedName = _selectedScope?.AliasName;
		Scopes.Clear();
		foreach (var s in scopes) Scopes.Add(s);

		var reselect = selectedName == null ? null : Scopes.FirstOrDefault(s => SameAlias(s.AliasName, selectedName));
		if (reselect != null) _selectedScope = reselect;
		OnPropertyChanged(nameof(SelectedScope));
	}

	private int CountFor(string aliasName) =>
		_all.Count(e => !e.IsGlobal && SameAlias(e.AliasName, aliasName));

	private void UpdateScopeCounts()
	{
		foreach (var scope in Scopes)
			scope.Count = scope.IsGlobal ? _all.Count(e => e.IsGlobal) : CountFor(scope.AliasName);
	}

	private IEnumerable<WordDictionaryEntry> EntriesFor(WordDictionaryScope scope) =>
		scope.IsGlobal
			? _all.Where(e => e.IsGlobal)
			: _all.Where(e => !e.IsGlobal && SameAlias(e.AliasName, scope.AliasName));

	private void LoadScopeEntries()
	{
		foreach (var e in Entries) e.PropertyChanged -= OnItemPropertyChanged;
		Entries.Clear();
		SelectedEntry = null;
		if (_selectedScope == null) return;
		foreach (var e in EntriesFor(_selectedScope).OrderBy(e => e.Word, StringComparer.OrdinalIgnoreCase))
		{
			e.PropertyChanged += OnItemPropertyChanged;
			Entries.Add(e);
		}
	}

	private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (_suppressItemChanges || sender is not WordDictionaryEntry entry || e.PropertyName == nameof(WordDictionaryEntry.IsGlobal)) return;
		_repo.Upsert(entry);
		if (entry == _selectedEntry) LoadEditor();
		ApplySample();
	}

	private void LoadEditor()
	{
		var e = _selectedEntry;
		EditWord        = e?.Word ?? string.Empty;
		EditReplacement = e?.Replacement ?? string.Empty;
		EditEnabled     = e?.Enabled ?? true;
	}

	private void AddEntry()
	{
		if (_selectedScope == null) return;
		var e = new WordDictionaryEntry
		{
			Word        = EditWord.Trim(),
			Replacement = EditReplacement.Trim(),
			AliasName   = _selectedScope.AliasName,
			Enabled     = EditEnabled,
		};
		_repo.Insert(e);
		_all.Add(e);

		e.PropertyChanged += OnItemPropertyChanged;
		Entries.Add(e);
		UpdateScopeCounts();
		SelectedEntry = Entries.FirstOrDefault(x => x == e);
		ApplySample();
	}

	private void DeleteEntry()
	{
		if (_selectedEntry == null) return;
		var e = _selectedEntry;
		_repo.Delete(e.Id);
		_all.Remove(e);
		e.PropertyChanged -= OnItemPropertyChanged;
		Entries.Remove(e);
		SelectedEntry = null;
		UpdateScopeCounts();
		ApplySample();
	}

	private void SaveEntry()
	{
		if (_selectedEntry == null) return;
		_suppressItemChanges = true;
		_selectedEntry.Word        = EditWord.Trim();
		_selectedEntry.Replacement = EditReplacement.Trim();
		_selectedEntry.Enabled     = EditEnabled;
		_suppressItemChanges = false;
		_repo.Upsert(_selectedEntry);
		ApplySample();
	}

	private void ApplySample()
	{
		SampleOutput = string.IsNullOrWhiteSpace(_sampleInput)
			? string.Empty
			: WordDictionaryApplier.Apply(_sampleInput, _selectedScope?.AliasName, _all);
		CommandManager.InvalidateRequerySuggested();
	}

	private async Task PlaySampleAsync()
	{
		if (string.IsNullOrWhiteSpace(_sampleOutput)) return;
		var settings = _settingsRepo.GetSettings();
		var aliasName = _selectedScope is { IsGlobal: false } ? _selectedScope.AliasName : settings.DefaultVoiceAlias;
		var resolved = _resolver.ResolveAlias(aliasName, settings);
		if (string.IsNullOrEmpty(resolved.VoiceId)) return;

		var text = resolved.LowercaseText ? _sampleOutput.ToLowerInvariant() : _sampleOutput;
		var audio = await resolved.Engine.SynthesizeAsync(text, resolved.VoiceId, resolved.Params);
		if (audio.IsEmpty) return;

		audio = AudioGain.Apply(audio, resolved.Volume);
		var player = new NAudioPlayer();
		await player.PlayAsync(audio, resolved.DeviceId, 100);
	}
}
