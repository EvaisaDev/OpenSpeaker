using System.Collections.ObjectModel;
using System.Windows;
using OpenSpeaker.Data;
using OpenSpeaker.Models;
namespace OpenSpeaker.ViewModels;

public class UsersViewModel : BaseViewModel
{
    private readonly UserRepository _repo;
    private readonly VoiceAliasRepository _aliasRepo;
    private static readonly TimeSpan PresenceWindow = TimeSpan.FromHours(1);

    private bool _hideNotPresent = true;
    public bool HideNotPresent
    {
        get => _hideNotPresent;
        set { SetField(ref _hideNotPresent, value); Refresh(); }
    }

    private List<UserRecord> _allUsers = new();
    private List<UserRecord> _allUsersUnfiltered = new();
    public IReadOnlyList<UserRecord> AllUsers => _allUsersUnfiltered;
    private List<string> _allAliasNames = new();
    public ObservableCollection<UserRecord> Users { get; } = new();
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

    private string _userFilter = string.Empty;
    public string UserFilter
    {
        get => _userFilter;
        set { SetField(ref _userFilter, value); ApplyFilter(); }
    }

    private UserRecord? _selectedUser;
    public UserRecord? SelectedUser
    {
        get => _selectedUser;
        set { SetField(ref _selectedUser, value); OnPropertyChanged(nameof(HasSelectedUser)); }
    }

    public bool HasSelectedUser => _selectedUser != null;

    public RelayCommand SaveUserCommand { get; }

    public UsersViewModel(UserRepository repo, VoiceAliasRepository aliasRepo)
    {
        _repo = repo;
        _aliasRepo = aliasRepo;
        SaveUserCommand = new RelayCommand(SaveUser, () => SelectedUser != null);
        RefreshAliases();
        Refresh();
    }

    public void RefreshAliases()
    {
        AliasNames.Clear();
        AliasNames.Add("<None>");
        foreach (var a in _aliasRepo.GetAllSorted())
            AliasNames.Add(a.Name);
        _allAliasNames = AliasNames.ToList();
        ApplyAliasFilter();
    }

    private void ApplyAliasFilter()
    {
        FilteredAliasNames = string.IsNullOrEmpty(_aliasFilter)
            ? _allAliasNames
            : _allAliasNames.Where(n => n.Contains(_aliasFilter, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public void Refresh()
    {
        var cutoff = DateTime.Now - PresenceWindow;
        _allUsersUnfiltered = _repo.GetAll().OrderBy(u => u.Username).ToList();
        _allUsers = _allUsersUnfiltered;
        if (_hideNotPresent)
            _allUsers = _allUsers.Where(u => u.LastActive >= cutoff).ToList();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var selectedId = _selectedUser?.TwitchId;
        var filtered = string.IsNullOrEmpty(_userFilter)
            ? _allUsers
            : _allUsers.Where(u =>
                (u.Username ?? "").Contains(_userFilter, StringComparison.OrdinalIgnoreCase) ||
                (u.Nickname ?? "").Contains(_userFilter, StringComparison.OrdinalIgnoreCase));
        Users.Clear();
        foreach (var u in filtered)
            Users.Add(u);
        if (selectedId != null)
            SelectedUser = Users.FirstOrDefault(u => u.TwitchId == selectedId);
    }

    public void OnChatMessage(string twitchId)
    {
        Application.Current?.Dispatcher.Invoke(Refresh);
    }

    private void SaveUser()
    {
        if (_selectedUser == null) return;
        _repo.Upsert(_selectedUser);
    }
}
