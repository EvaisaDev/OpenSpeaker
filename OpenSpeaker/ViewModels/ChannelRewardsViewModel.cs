using System.Collections.ObjectModel;
using System.Windows;
using OpenSpeaker.Data;
using OpenSpeaker.Models;
using OpenSpeaker.Twitch;
namespace OpenSpeaker.ViewModels;

public class ChannelRewardsViewModel : BaseViewModel
{
    private readonly ChannelRewardRepository _repo;
    private readonly TwitchAuthService _twitchAuth;

    public ObservableCollection<ChannelReward> Rewards { get; } = new();
    public ObservableCollection<string> AliasNames { get; } = new();
    public ObservableCollection<EventMessageViewModel> Messages { get; } = new();

    private ChannelReward? _selectedReward;
    public ChannelReward? SelectedReward
    {
        get => _selectedReward;
        set { SetField(ref _selectedReward, value); OnPropertyChanged(nameof(HasSelected)); LoadMessages(); }
    }

    public bool HasSelected => _selectedReward != null;

    private double _editWeight = 1;
    private bool _editEnabled = true;
    private string _editTemplate = string.Empty;

    public double EditWeight { get => _editWeight; set => SetField(ref _editWeight, value); }
    public bool EditEnabled { get => _editEnabled; set => SetField(ref _editEnabled, value); }
    public string EditTemplate { get => _editTemplate; set => SetField(ref _editTemplate, value); }

    private EventMessageViewModel? _selectedMessage;
    public EventMessageViewModel? SelectedMessage
    {
        get => _selectedMessage;
        set { SetField(ref _selectedMessage, value); if (value != null) { EditWeight = value.Weight; EditEnabled = value.Enabled; EditTemplate = value.Template; } }
    }

    private bool _refreshing;
    public bool Refreshing { get => _refreshing; set => SetField(ref _refreshing, value); }

    public RelayCommand RefreshFromTwitchCommand { get; }
    public RelayCommand SaveRewardCommand { get; }
    public RelayCommand RemoveRewardCommand { get; }
    public RelayCommand AddMessageCommand { get; }
    public RelayCommand DeleteMessageCommand { get; }
    public RelayCommand SaveMessageCommand { get; }

    public ChannelRewardsViewModel(ChannelRewardRepository repo, TwitchAuthService twitchAuth, VoiceAliasRepository aliasRepo)
    {
        _repo = repo;
        _twitchAuth = twitchAuth;

        AliasNames.Add(string.Empty);
        foreach (var a in aliasRepo.GetAllSorted())
            AliasNames.Add(a.Name);

        RefreshFromTwitchCommand = new RelayCommand(async () => await RefreshFromTwitchAsync(), () => !Refreshing);
        SaveRewardCommand = new RelayCommand(SaveReward, () => SelectedReward != null);
        RemoveRewardCommand = new RelayCommand(RemoveReward, () => SelectedReward != null);
        AddMessageCommand = new RelayCommand(AddMessage, () => SelectedReward != null);
        DeleteMessageCommand = new RelayCommand(DeleteMessage, () => SelectedMessage != null);
        SaveMessageCommand = new RelayCommand(SaveMessage, () => SelectedMessage != null);

        Refresh();
        if (_twitchAuth.HasValidAccount())
            _ = RefreshFromTwitchAsync();
    }

    public void Refresh()
    {
        Rewards.Clear();
        foreach (var r in _repo.GetAll())
            Rewards.Add(r);
    }

    private void LoadMessages()
    {
        Messages.Clear();
        if (_selectedReward == null) return;
        foreach (var m in _selectedReward.Messages)
            Messages.Add(new EventMessageViewModel(m));
    }

    private async Task RefreshFromTwitchAsync()
    {
        try
        {
            Refreshing = true;
            var account = _twitchAuth.GetAccount();
            if (account == null) return;

            var api = _twitchAuth.CreateApi();
            var response = await api.Helix.ChannelPoints.GetCustomRewardAsync(account.UserId);
            if (response?.Data == null) return;

            foreach (var reward in response.Data)
            {
                var existing = _repo.GetByTwitchId(reward.Id) ?? new ChannelReward { TwitchRewardId = reward.Id };
                existing.Title = reward.Title;
                existing.Cost = reward.Cost;
                _repo.Upsert(existing);
            }

            Application.Current?.Dispatcher.Invoke(Refresh);
        }
        catch { }
        finally { Refreshing = false; }
    }

    private void SaveReward()
    {
        if (_selectedReward == null) return;
        _repo.Upsert(_selectedReward);
    }

    private void RemoveReward()
    {
        if (_selectedReward == null) return;
        _repo.Delete(_selectedReward.Id);
        Refresh();
        SelectedReward = null;
    }

    private void AddMessage()
    {
        if (_selectedReward == null) return;
        var msg = new EventMessage { Template = string.Empty, Weight = 1, Enabled = true };
        _selectedReward.Messages.Add(msg);
        _repo.Upsert(_selectedReward);
        var vm = new EventMessageViewModel(msg);
        Messages.Add(vm);
        SelectedMessage = vm;
    }

    private void DeleteMessage()
    {
        if (_selectedMessage == null || _selectedReward == null) return;
        _selectedReward.Messages.Remove(_selectedMessage.Model);
        _repo.Upsert(_selectedReward);
        Messages.Remove(_selectedMessage);
        SelectedMessage = null;
    }

    private void SaveMessage()
    {
        if (_selectedMessage == null || _selectedReward == null) return;
        _selectedMessage.Weight = (int)EditWeight;
        _selectedMessage.Enabled = EditEnabled;
        _selectedMessage.Template = EditTemplate;
        _repo.Upsert(_selectedReward);
    }
}
