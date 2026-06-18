using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using LiteDB;
using OpenSpeaker.Data;
using OpenSpeaker.Models;
namespace OpenSpeaker.ViewModels;

public class ReplacementViewModel : BaseViewModel
{
    private readonly DatabaseContext _db;

    public ObservableCollection<RegexReplacement> Replacements { get; } = new();

    public IEnumerable<string> AvailableModes { get; } = new[] { "Replace", "Skip" };

    private RegexReplacement? _selectedReplacement;
    public RegexReplacement? SelectedReplacement
    {
        get => _selectedReplacement;
        set { SetField(ref _selectedReplacement, value); LoadEditor(); }
    }

    private string _editMode = "Replace";
    public string EditMode
    {
        get => _editMode;
        set { SetField(ref _editMode, value); OnPropertyChanged(nameof(IsReplaceMode)); }
    }

    private string _editPattern = string.Empty;
    public string EditPattern { get => _editPattern; set => SetField(ref _editPattern, value); }

    private string _editReplacement = string.Empty;
    public string EditReplacement { get => _editReplacement; set => SetField(ref _editReplacement, value); }

    private bool _editIsRegex = true;
    public bool EditIsRegex { get => _editIsRegex; set => SetField(ref _editIsRegex, value); }

    private bool _editEnabled = true;
    public bool EditEnabled { get => _editEnabled; set => SetField(ref _editEnabled, value); }

    public bool IsReplaceMode => _editMode == "Replace";

    private string _sampleInput = string.Empty;
    public string SampleInput { get => _sampleInput; set { SetField(ref _sampleInput, value); ApplySample(); } }

    private string _sampleOutput = string.Empty;
    public string SampleOutput { get => _sampleOutput; private set => SetField(ref _sampleOutput, value); }

    public RelayCommand AddReplacementCommand { get; }
    public RelayCommand DeleteReplacementCommand { get; }
    public RelayCommand SaveReplacementCommand { get; }

    public ReplacementViewModel(DatabaseContext db, SettingsRepository settingsRepo)
    {
        _db = db;

        AddReplacementCommand = new RelayCommand(AddReplacement, () => !string.IsNullOrWhiteSpace(EditPattern));
        DeleteReplacementCommand = new RelayCommand(DeleteReplacement, () => SelectedReplacement != null);
        SaveReplacementCommand = new RelayCommand(SaveReplacement, () => SelectedReplacement != null);

        Refresh();
    }

    public void Refresh()
    {
        Replacements.Clear();
        foreach (var r in _db.RegexReplacements.FindAll())
            Replacements.Add(r);
    }

    private void LoadEditor()
    {
        if (_selectedReplacement == null) return;
        EditMode = string.IsNullOrEmpty(_selectedReplacement.Mode) ? "Replace" : _selectedReplacement.Mode;
        EditPattern = _selectedReplacement.Pattern;
        EditReplacement = _selectedReplacement.Replacement;
        EditIsRegex = _selectedReplacement.IsRegex;
        EditEnabled = _selectedReplacement.Enabled;
    }

    private void AddReplacement()
    {
        var r = new RegexReplacement
        {
            Mode = EditMode,
            Pattern = EditPattern,
            Replacement = EditReplacement,
            IsRegex = EditIsRegex,
            Enabled = EditEnabled
        };
        _db.RegexReplacements.Insert(r);
        Replacements.Add(r);
        SelectedReplacement = r;
    }

    private void DeleteReplacement()
    {
        if (_selectedReplacement == null) return;
        _db.RegexReplacements.Delete(_selectedReplacement.Id);
        Replacements.Remove(_selectedReplacement);
        SelectedReplacement = null;
    }

    private void SaveReplacement()
    {
        if (_selectedReplacement == null) return;
        _selectedReplacement.Mode = EditMode;
        _selectedReplacement.Pattern = EditPattern;
        _selectedReplacement.Replacement = EditReplacement;
        _selectedReplacement.IsRegex = EditIsRegex;
        _selectedReplacement.Enabled = EditEnabled;
        _db.RegexReplacements.Upsert(_selectedReplacement);
    }

    private void ApplySample()
    {
        var text = _sampleInput;
        foreach (var r in Replacements.Where(r => r.Enabled))
        {
            if (string.IsNullOrEmpty(r.Pattern)) continue;
            var mode = string.IsNullOrEmpty(r.Mode) ? "Replace" : r.Mode;
            if (mode == "Skip")
            {
                bool skip;
                if (r.IsRegex) { try { skip = Regex.IsMatch(text, r.Pattern); } catch { continue; } }
                else { skip = text.Contains(r.Pattern, StringComparison.OrdinalIgnoreCase); }
                if (skip) { text = string.Empty; break; }
            }
            else
            {
                if (r.IsRegex) { try { text = Regex.Replace(text, r.Pattern, r.Replacement); } catch { } }
                else { text = text.Replace(r.Pattern, r.Replacement, StringComparison.OrdinalIgnoreCase); }
            }
        }
        SampleOutput = text;
    }
}
