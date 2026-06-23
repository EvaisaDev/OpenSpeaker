using System.Collections.ObjectModel;
using OpenSpeaker.Data;
using OpenSpeaker.Models;
namespace OpenSpeaker.ViewModels;

public class CustomCommandsViewModel : BaseViewModel
{
    private readonly CustomCommandRepository _repo;

    public ObservableCollection<CustomCommand> Commands { get; } = new();
    public ObservableCollection<string> AliasNames { get; } = new();

    private CustomCommand? _selectedCommand;
    public CustomCommand? SelectedCommand
    {
        get => _selectedCommand;
        set
        {
            SetField(ref _selectedCommand, value);
            OnPropertyChanged(nameof(IsEditing));
            OnPropertyChanged(nameof(AddSaveLabel));
            PopulateForm(value);
        }
    }

    public bool IsEditing => _selectedCommand != null;
    public string AddSaveLabel => _selectedCommand != null ? "Save" : "Add";

    private string _newTrigger = string.Empty;
    public string NewTrigger { get => _newTrigger; set => SetField(ref _newTrigger, value); }

    private string _newAlias = "<None>";
    public string NewAlias { get => _newAlias; set => SetField(ref _newAlias, value); }

    private bool _newAllowModerators = true;
    private bool _newAllowSubscribers = true;
    private bool _newAllowVips = true;
    private bool _newAllowRegulars = true;
    private bool _newAllowEveryone = false;

    public bool NewAllowModerators { get => _newAllowModerators; set => SetField(ref _newAllowModerators, value); }
    public bool NewAllowSubscribers { get => _newAllowSubscribers; set => SetField(ref _newAllowSubscribers, value); }
    public bool NewAllowVIPs { get => _newAllowVips; set => SetField(ref _newAllowVips, value); }
    public bool NewAllowRegulars { get => _newAllowRegulars; set => SetField(ref _newAllowRegulars, value); }
    public bool NewAllowEveryone { get => _newAllowEveryone; set => SetField(ref _newAllowEveryone, value); }

    public RelayCommand AddSaveCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand NewCommand { get; }
    public RelayCommand ToggleEnabledCommand { get; }

    public CustomCommandsViewModel(CustomCommandRepository repo, VoiceAliasRepository aliasRepo)
    {
        _repo = repo;
        AddSaveCommand = new RelayCommand(AddOrSave, () => !string.IsNullOrWhiteSpace(NewTrigger));
        DeleteCommand = new RelayCommand(Delete, () => SelectedCommand != null);
        NewCommand = new RelayCommand(() => SelectedCommand = null);
        ToggleEnabledCommand = new RelayCommand(p =>
        {
            if (p is not CustomCommand cmd) return;
            _repo.Upsert(cmd);
        });

        AliasNames.Add("<None>");
        foreach (var a in aliasRepo.GetAllSorted())
            AliasNames.Add(a.Name);

        Refresh();
    }

    public void Refresh()
    {
        var selectedId = _selectedCommand?.Id;
        Commands.Clear();
        foreach (var c in _repo.GetAll())
            Commands.Add(c);
        SelectedCommand = selectedId != null ? Commands.FirstOrDefault(c => c.Id == selectedId) : null;
    }

    private void PopulateForm(CustomCommand? cmd)
    {
        if (cmd == null)
        {
            NewTrigger = string.Empty;
            NewAlias = "<None>";
            NewAllowModerators = true;
            NewAllowSubscribers = true;
            NewAllowVIPs = true;
            NewAllowRegulars = true;
            NewAllowEveryone = false;
        }
        else
        {
            NewTrigger = cmd.Trigger;
            NewAlias = string.IsNullOrEmpty(cmd.VoiceAliasName) ? "<None>" : cmd.VoiceAliasName;
            NewAllowModerators = cmd.AllowedRoles.Contains(UserRoles.Moderator);
            NewAllowSubscribers = cmd.AllowedRoles.Contains(UserRoles.Subscriber);
            NewAllowVIPs = cmd.AllowedRoles.Contains(UserRoles.VIP);
            NewAllowRegulars = cmd.AllowedRoles.Contains(UserRoles.Regular);
            NewAllowEveryone = cmd.AllowedRoles.Contains(UserRoles.Everyone);
        }
    }

    private List<string> BuildRoles()
    {
        var roles = new List<string>();
        if (NewAllowModerators) roles.Add(UserRoles.Moderator);
        if (NewAllowSubscribers) roles.Add(UserRoles.Subscriber);
        if (NewAllowVIPs) roles.Add(UserRoles.VIP);
        if (NewAllowRegulars) roles.Add(UserRoles.Regular);
        if (NewAllowEveryone) roles.Add(UserRoles.Everyone);
        return roles;
    }

    private void AddOrSave()
    {
        if (string.IsNullOrWhiteSpace(NewTrigger)) return;
        var trigger = NewTrigger.StartsWith("!") ? NewTrigger : "!" + NewTrigger;
        var alias = NewAlias == "<None>" ? string.Empty : NewAlias;
        var roles = BuildRoles();

        if (_selectedCommand != null)
        {
            _selectedCommand.Trigger = trigger;
            _selectedCommand.VoiceAliasName = alias;
            _selectedCommand.AllowedRoles = roles;
            _repo.Upsert(_selectedCommand);
            Refresh();
        }
        else
        {
            _repo.Upsert(new CustomCommand { Trigger = trigger, VoiceAliasName = alias, AllowedRoles = roles, Enabled = true });
            NewTrigger = string.Empty;
            Refresh();
        }
    }

    private void Delete()
    {
        if (_selectedCommand == null) return;
        _repo.Delete(_selectedCommand.Id);
        SelectedCommand = null;
        Refresh();
    }
}
