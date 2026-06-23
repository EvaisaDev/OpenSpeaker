using System.Collections.ObjectModel;
using LiteDB;
using OpenSpeaker.Data;
using OpenSpeaker.Models;
using OpenSpeaker.TTS;
namespace OpenSpeaker.ViewModels;

public class CustomApiViewModel : BaseViewModel
{
    private readonly DatabaseContext _db;
    private readonly TtsEngineRegistry _registry;
    private CustomApiDefinition? _pendingNew;

    public ObservableCollection<CustomApiDefinition> Definitions { get; } = new();
    public CustomApiEditViewModel Edit { get; } = new();

    private CustomApiDefinition? _selected;
    public CustomApiDefinition? Selected
    {
        get => _selected;
        set
        {
            if (_pendingNew != null && _pendingNew != value)
            {
                Definitions.Remove(_pendingNew);
                _pendingNew = null;
            }
            SetField(ref _selected, value);
            OnPropertyChanged(nameof(HasSelected));
            if (_selected != null)
                Edit.LoadFrom(_selected);
        }
    }

    public bool HasSelected => _selected != null;

    public RelayCommand AddCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand DeleteCommand { get; }

    public CustomApiViewModel(DatabaseContext db, TtsEngineRegistry registry)
    {
        _db = db;
        _registry = registry;
        AddCommand = new RelayCommand(Add);
        SaveCommand = new RelayCommand(Save, () => Selected != null);
        DeleteCommand = new RelayCommand(Delete, () => Selected != null);
        Refresh();
    }

    private void Add()
    {
        var def = new CustomApiDefinition { Name = "New API" };
        _pendingNew = def;
        Definitions.Add(def);
        Selected = def;
    }

    private void Save()
    {
        if (_selected == null) return;
        Edit.ApplyTo(_selected);
        _db.CustomApis.Upsert(_selected);
        _pendingNew = null;
        _registry.Reload();
        var savedId = _selected.Id;
        Refresh(savedId);
    }

    private void Delete()
    {
        if (_selected == null) return;
        if (_selected != _pendingNew)
            _db.CustomApis.Delete(_selected.Id);
        _pendingNew = null;
        _registry.Reload();
        Selected = null;
        Refresh();
    }

    private void Refresh(ObjectId? selectId = null)
    {
        Definitions.Clear();
        foreach (var d in _db.CustomApis.FindAll().OrderBy(d => d.Name))
            Definitions.Add(d);
        Selected = selectId != null ? Definitions.FirstOrDefault(d => d.Id == selectId) : null;
    }
}
