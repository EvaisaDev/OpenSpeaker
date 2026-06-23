using System.Collections.ObjectModel;
using OpenSpeaker.Data;
using OpenSpeaker.Models;
namespace OpenSpeaker.ViewModels;

public class EventTypeItem
{
    public string EventType { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}

public class EventMessageViewModel : BaseViewModel
{
    private EventMessage _model;
    public EventMessage Model => _model;

    public int Weight { get => _model.Weight; set { _model.Weight = value; OnPropertyChanged(); } }
    public bool Enabled { get => _model.Enabled; set { _model.Enabled = value; OnPropertyChanged(); } }
    public string Template { get => _model.Template; set { _model.Template = value; OnPropertyChanged(); } }

    public EventMessageViewModel(EventMessage model) { _model = model; }
}

public class EventsViewModel : SettingsViewModelBase
{
    private readonly EventConfigRepository _repo;

    public ObservableCollection<EventTypeItem> EventTypes { get; } = new();
    public ObservableCollection<EventMessageViewModel> Messages { get; } = new();
    public ObservableCollection<string> AliasNames { get; } = new();

    private EventTypeItem? _selectedEventType;
    public EventTypeItem? SelectedEventType
    {
        get => _selectedEventType;
        set { SetField(ref _selectedEventType, value); LoadEvent(); }
    }

    private EventConfig? _currentConfig;

    public bool EventEnabled
    {
        get => _currentConfig?.Enabled ?? false;
        set { if (_currentConfig != null) { _currentConfig.Enabled = value; _repo.Upsert(_currentConfig); OnPropertyChanged(); } }
    }

    public string EventVoiceAlias
    {
        get => _currentConfig?.VoiceAliasOverride ?? string.Empty;
        set { if (_currentConfig != null) { _currentConfig.VoiceAliasOverride = value; _repo.Upsert(_currentConfig); OnPropertyChanged(); } }
    }

    private EventMessageViewModel? _selectedMessage;
    public EventMessageViewModel? SelectedMessage
    {
        get => _selectedMessage;
        set { SetField(ref _selectedMessage, value); LoadMessageEditor(); }
    }

    private double _editWeight = 0;
    private bool _editEnabled = true;
    private string _editTemplate = string.Empty;

    public double EditWeight { get => _editWeight; set => SetField(ref _editWeight, value); }
    public bool EditEnabled { get => _editEnabled; set => SetField(ref _editEnabled, value); }
    public string EditTemplate { get => _editTemplate; set => SetField(ref _editTemplate, value); }

    public bool GlobalEventsEnabled
    {
        get => Settings.EventsEnabled;
        set => Set(s => s.EventsEnabled = value);
    }

    public string GlobalVoiceAlias
    {
        get => Settings.GlobalEventVoiceAlias;
        set => Set(s => s.GlobalEventVoiceAlias = value);
    }

    public bool FollowsEnabled { get => GetState(Models.EventTypes.Follow).Enabled; set { GetState(Models.EventTypes.Follow).Enabled = value; SaveEvent(Models.EventTypes.Follow); OnPropertyChanged(); } }
    public bool SubscriptionsEnabled { get => GetState(Models.EventTypes.Sub).Enabled; set { GetState(Models.EventTypes.Sub).Enabled = value; SaveEvent(Models.EventTypes.Sub); OnPropertyChanged(); } }
    public bool GiftBombsEnabled { get => GetState(Models.EventTypes.GiftBomb).Enabled; set { GetState(Models.EventTypes.GiftBomb).Enabled = value; SaveEvent(Models.EventTypes.GiftBomb); OnPropertyChanged(); } }
    public bool RaidsEnabled { get => GetState(Models.EventTypes.Raid).Enabled; set { GetState(Models.EventTypes.Raid).Enabled = value; SaveEvent(Models.EventTypes.Raid); OnPropertyChanged(); } }
    public bool CheersEnabled { get => GetState(Models.EventTypes.Cheer).Enabled; set { GetState(Models.EventTypes.Cheer).Enabled = value; SaveEvent(Models.EventTypes.Cheer); OnPropertyChanged(); } }
    public bool HypeTrainsEnabled { get => GetState(Models.EventTypes.HypeTrain).Enabled; set { GetState(Models.EventTypes.HypeTrain).Enabled = value; SaveEvent(Models.EventTypes.HypeTrain); OnPropertyChanged(); } }
    public bool GoalEnabled { get => GetState(Models.EventTypes.Goal).Enabled; set { GetState(Models.EventTypes.Goal).Enabled = value; SaveEvent(Models.EventTypes.Goal); OnPropertyChanged(); } }
    public bool ChannelPointsEnabled { get => GetState(Models.EventTypes.ChannelPoint).Enabled; set { GetState(Models.EventTypes.ChannelPoint).Enabled = value; SaveEvent(Models.EventTypes.ChannelPoint); OnPropertyChanged(); } }

    public int MinRaidViewers { get => GetState(Models.EventTypes.Raid).MinRaidViewers; set { GetState(Models.EventTypes.Raid).MinRaidViewers = value; SaveEvent(Models.EventTypes.Raid); OnPropertyChanged(); } }
    public int MinBits { get => GetState(Models.EventTypes.Cheer).MinBits; set { GetState(Models.EventTypes.Cheer).MinBits = value; SaveEvent(Models.EventTypes.Cheer); OnPropertyChanged(); } }

    public RelayCommand AddMessageCommand { get; }
    public RelayCommand DeleteMessageCommand { get; }
    public RelayCommand SaveMessageCommand { get; }

    public EventsViewModel(EventConfigRepository repo, SettingsRepository settingsRepo, VoiceAliasRepository aliasRepo)
        : base(settingsRepo)
    {
        _repo = repo;

        _repo.EnsureAllEventTypes();

        AliasNames.Add("<None>");
        foreach (var a in aliasRepo.GetAllSorted())
            AliasNames.Add(a.Name);

        var items = new (string, string)[]
        {
            (Models.EventTypes.Follow, "Follow"),
            (Models.EventTypes.Sub, "Sub"),
            (Models.EventTypes.Resub, "Resub"),
            (Models.EventTypes.GiftSub, "GiftSub"),
            (Models.EventTypes.GiftBomb, "GiftBomb"),
            (Models.EventTypes.Cheer, "Cheer"),
            (Models.EventTypes.Raid, "Raid"),
            (Models.EventTypes.ChannelPoint, "ChannelPoints"),
            (Models.EventTypes.HypeTrain, "HypeTrain"),
            (Models.EventTypes.Goal, "Goal"),
        };

        foreach (var (type, displayName) in items)
            EventTypes.Add(new EventTypeItem { EventType = type, DisplayName = displayName });

        AddMessageCommand = new RelayCommand(AddMessage, () => _currentConfig != null);
        DeleteMessageCommand = new RelayCommand(DeleteMessage, () => SelectedMessage != null);
        SaveMessageCommand = new RelayCommand(SaveMessage, () => SelectedMessage != null);

        SelectedEventType = EventTypes.FirstOrDefault();
    }

    private EventState GetState(string type)
    {
        var cfg = _repo.GetByEventType(type);
        return cfg?.State ?? new EventState();
    }

    private void SaveEvent(string type)
    {
        var cfg = _repo.GetByEventType(type);
        if (cfg != null) _repo.Upsert(cfg);
    }

    public override void Refresh()
    {
        Settings = SettingsRepo.GetSettings();
        OnPropertyChanged(nameof(GlobalEventsEnabled));
        OnPropertyChanged(nameof(GlobalVoiceAlias));
        LoadEvent();
    }

    private void LoadEvent()
    {
        if (_selectedEventType == null) return;
        _currentConfig = _repo.GetByEventType(_selectedEventType.EventType);
        Messages.Clear();
        if (_currentConfig != null)
        {
            foreach (var m in _currentConfig.Messages)
                Messages.Add(new EventMessageViewModel(m));
        }
        OnPropertyChanged(nameof(EventEnabled));
        OnPropertyChanged(nameof(EventVoiceAlias));
        SelectedMessage = null;
    }

    private void LoadMessageEditor()
    {
        if (_selectedMessage == null) return;
        EditWeight = _selectedMessage.Weight;
        EditEnabled = _selectedMessage.Enabled;
        EditTemplate = _selectedMessage.Template;
    }

    private void AddMessage()
    {
        if (_currentConfig == null) return;
        var msg = new EventMessage { Template = string.Empty, Weight = 1, Enabled = true };
        _currentConfig.Messages.Add(msg);
        _repo.Upsert(_currentConfig);
        var vm = new EventMessageViewModel(msg);
        Messages.Add(vm);
        SelectedMessage = vm;
    }

    private void DeleteMessage()
    {
        if (_selectedMessage == null || _currentConfig == null) return;
        _currentConfig.Messages.Remove(_selectedMessage.Model);
        _repo.Upsert(_currentConfig);
        Messages.Remove(_selectedMessage);
        SelectedMessage = null;
    }

    private void SaveMessage()
    {
        if (_selectedMessage == null || _currentConfig == null) return;
        _selectedMessage.Weight = (int)EditWeight;
        _selectedMessage.Enabled = EditEnabled;
        _selectedMessage.Template = EditTemplate;
        _repo.Upsert(_currentConfig);
    }

    public void SaveCurrentEvent()
    {
        if (_currentConfig == null) return;
        _repo.Upsert(_currentConfig);
    }
}
