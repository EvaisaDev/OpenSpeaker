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

    public ObservableCollection<UserRecord> Users { get; } = new();
    public ObservableCollection<string> AliasNames { get; } = new();

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
    }

    public void Refresh()
    {
        var cutoff = DateTime.Now - PresenceWindow;
        var all = _repo.GetAll().OrderBy(u => u.Username).ToList();
        Users.Clear();
        foreach (var u in all)
        {
            if (_hideNotPresent && u.LastActive < cutoff) continue;
            Users.Add(u);
        }
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
