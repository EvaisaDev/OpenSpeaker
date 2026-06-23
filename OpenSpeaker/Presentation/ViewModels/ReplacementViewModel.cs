using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Windows.Input;
using OpenSpeaker.Data;
using OpenSpeaker.Models;
namespace OpenSpeaker.ViewModels;

public class ReplacementViewModel : BaseViewModel
{
    private readonly RegexReplacementRepository _regexRepo;

    public ObservableCollection<RegexReplacement> Replacements { get; } = new();

    public IEnumerable<string> AvailableModes { get; } = new[] { "Replace", "Skip" };

    private List<RegexReplacement> _selectedItems = new();

    private RegexReplacement? _selectedReplacement;
    public RegexReplacement? SelectedReplacement
    {
        get => _selectedReplacement;
        set => SetField(ref _selectedReplacement, value);
    }

    private string? _editMode = "Replace";
    public string? EditMode
    {
        get => _editMode;
        set { SetField(ref _editMode, value); OnPropertyChanged(nameof(IsReplaceMode)); }
    }

    private string _editPattern = string.Empty;
    public string EditPattern
    {
        get => _editPattern;
        set { SetField(ref _editPattern, value); if (!string.IsNullOrEmpty(value)) EditPatternMixed = false; }
    }

    private string _editReplacement = string.Empty;
    public string EditReplacement
    {
        get => _editReplacement;
        set { SetField(ref _editReplacement, value); if (!string.IsNullOrEmpty(value)) EditReplacementMixed = false; }
    }

    private bool? _editIsRegex = true;
    public bool? EditIsRegex { get => _editIsRegex; set { SetField(ref _editIsRegex, value); OnPropertyChanged(nameof(WholeWordVisible)); } }

    private bool? _editWholeWord = false;
    public bool? EditWholeWord { get => _editWholeWord; set => SetField(ref _editWholeWord, value); }

    private bool? _editEnabled = true;
    public bool? EditEnabled { get => _editEnabled; set => SetField(ref _editEnabled, value); }

    public bool WholeWordVisible => _editIsRegex != true;

    private bool _editPatternMixed;
    public bool EditPatternMixed { get => _editPatternMixed; private set => SetField(ref _editPatternMixed, value); }

    private bool _editReplacementMixed;
    public bool EditReplacementMixed { get => _editReplacementMixed; private set => SetField(ref _editReplacementMixed, value); }

    public bool IsReplaceMode => _editMode == "Replace";

    private string _sampleInput = string.Empty;
    public string SampleInput { get => _sampleInput; set { SetField(ref _sampleInput, value); ApplySample(); } }

    private string _sampleOutput = string.Empty;
    public string SampleOutput { get => _sampleOutput; private set => SetField(ref _sampleOutput, value); }

    public RelayCommand AddReplacementCommand { get; }
    public RelayCommand DeleteReplacementCommand { get; }
    public RelayCommand SaveReplacementCommand { get; }
    public RelayCommand ImportWordlistCommand { get; }

    private readonly IDialogService _dialogs;

    public ReplacementViewModel(RegexReplacementRepository regexRepo, SettingsRepository settingsRepo, IDialogService? dialogs = null)
    {
        _regexRepo = regexRepo;
        _dialogs = dialogs ?? new DialogService();

        AddReplacementCommand    = new RelayCommand(AddReplacement,    () => !string.IsNullOrWhiteSpace(EditPattern));
        DeleteReplacementCommand = new RelayCommand(DeleteReplacement, () => _selectedItems.Count > 0);
        SaveReplacementCommand   = new RelayCommand(SaveReplacement,   () => _selectedItems.Count > 0);
        ImportWordlistCommand    = new RelayCommand(ImportWordlist);

        Refresh();
    }

    public void OnSelectionChanged(IList items)
    {
        _selectedItems = items.Cast<RegexReplacement>().ToList();
        LoadEditor();
        CommandManager.InvalidateRequerySuggested();
    }

    public void Refresh()
    {
        Replacements.Clear();
        foreach (var r in _regexRepo.GetAll().OrderBy(r => r.Order))
            Replacements.Add(r);
    }

    private void LoadEditor()
    {
        if (_selectedItems.Count == 0)
        {
            EditMode            = "Replace";
            EditPattern         = string.Empty;
            EditReplacement     = string.Empty;
            EditIsRegex         = true;
            EditWholeWord        = false;
            EditEnabled         = true;
            EditPatternMixed     = false;
            EditReplacementMixed = false;
            return;
        }

        var first     = _selectedItems[0];
        var firstMode = string.IsNullOrEmpty(first.Mode) ? "Replace" : first.Mode;

        EditMode = _selectedItems.All(r => (r.Mode ?? "Replace") == firstMode) ? firstMode : null;

        var allSamePattern = _selectedItems.All(r => r.Pattern == first.Pattern);
        EditPatternMixed = !allSamePattern;
        EditPattern      = allSamePattern ? first.Pattern : string.Empty;

        var allSameReplacement = _selectedItems.All(r => r.Replacement == first.Replacement);
        EditReplacementMixed = !allSameReplacement;
        EditReplacement      = allSameReplacement ? first.Replacement : string.Empty;

        var allSameRegex = _selectedItems.All(r => r.IsRegex == first.IsRegex);
        EditIsRegex = allSameRegex ? first.IsRegex : (bool?)null;

        var allSameWholeWord = _selectedItems.All(r => r.WholeWord == first.WholeWord);
        EditWholeWord = allSameWholeWord ? first.WholeWord : (bool?)null;

        var allSameEnabled = _selectedItems.All(r => r.Enabled == first.Enabled);
        EditEnabled = allSameEnabled ? first.Enabled : (bool?)null;
    }

    private void AddReplacement()
    {
        var r = new RegexReplacement
        {
            Mode        = EditMode ?? "Replace",
            Pattern     = EditPattern,
            Replacement = EditReplacement,
            IsRegex     = EditIsRegex ?? true,
            WholeWord   = EditWholeWord ?? false,
            Enabled     = EditEnabled ?? true,
            Order       = Replacements.Count,
        };
        _regexRepo.Insert(r);
        Replacements.Add(r);
        SelectedReplacement = r;
        _selectedItems = new List<RegexReplacement> { r };
        CommandManager.InvalidateRequerySuggested();
    }

    private void DeleteReplacement()
    {
        var toDelete = _selectedItems.ToList();
        foreach (var r in toDelete)
        {
            _regexRepo.Delete(r.Id);
            Replacements.Remove(r);
        }
        _selectedItems.Clear();
        SelectedReplacement = null;
        LoadEditor();
        CommandManager.InvalidateRequerySuggested();
    }

    private void SaveReplacement()
    {
        if (_selectedItems.Count == 0) return;

        foreach (var r in _selectedItems)
        {
            if (EditMode != null) r.Mode = EditMode;
            if (!EditPatternMixed || !string.IsNullOrEmpty(EditPattern)) r.Pattern = EditPattern;
            var targetMode = EditMode ?? r.Mode ?? "Replace";
            if (targetMode == "Replace" && (!EditReplacementMixed || !string.IsNullOrEmpty(EditReplacement)))
                r.Replacement = EditReplacement;
            if (EditIsRegex.HasValue) r.IsRegex = EditIsRegex.Value;
            if (EditWholeWord.HasValue) r.WholeWord = EditWholeWord.Value;
            if (EditEnabled.HasValue) r.Enabled = EditEnabled.Value;
            _regexRepo.Upsert(r);
        }
    }

    private void ImportWordlist()
    {
        var path = _dialogs.PickFile("Text files (*.txt)|*.txt|All files (*.*)|*.*", "Import Word List");
        if (path == null) return;

        var lines     = File.ReadAllLines(path)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();
        var nextOrder = _regexRepo.Count();
        foreach (var word in lines)
        {
            _regexRepo.Insert(new RegexReplacement
            {
                Pattern     = word,
                Replacement = string.Empty,
                IsRegex     = false,
                Mode        = "Skip",
                Enabled     = true,
                Order       = nextOrder++,
            });
        }
        Refresh();
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
                if (r.IsRegex) { try { skip = Regex.IsMatch(text, r.Pattern, RegexOptions.IgnoreCase); } catch { continue; } }
                else if (r.WholeWord) { try { skip = Regex.IsMatch(text, $@"\b{Regex.Escape(r.Pattern)}\b", RegexOptions.IgnoreCase); } catch { continue; } }
                else { skip = text.Contains(r.Pattern, StringComparison.OrdinalIgnoreCase); }
                if (skip) { text = string.Empty; break; }
            }
            else
            {
                if (r.IsRegex) { try { text = Regex.Replace(text, r.Pattern, r.Replacement, RegexOptions.IgnoreCase); } catch { } }
                else if (r.WholeWord) { try { text = Regex.Replace(text, $@"\b{Regex.Escape(r.Pattern)}\b", r.Replacement, RegexOptions.IgnoreCase); } catch { } }
                else { text = text.Replace(r.Pattern, r.Replacement, StringComparison.OrdinalIgnoreCase); }
            }
        }
        SampleOutput = text;
    }
}
