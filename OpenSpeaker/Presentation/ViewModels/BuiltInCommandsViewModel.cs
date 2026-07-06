using System.Collections.ObjectModel;
using OpenSpeaker.Chat;
using OpenSpeaker.Data;
using OpenSpeaker.Models;
namespace OpenSpeaker.ViewModels;

public class BuiltInCommandsViewModel : BaseViewModel
{
    private readonly SettingsRepository _settingsRepo;

    public ObservableCollection<BuiltInCommandConfig> Commands { get; } = new();

    private BuiltInCommandConfig? _selectedCommand;
    public BuiltInCommandConfig? SelectedCommand
    {
        get => _selectedCommand;
        set
        {
            SetField(ref _selectedCommand, value);
            OnPropertyChanged(nameof(IsEditing));
            OnPropertyChanged(nameof(PlaceholderHint));
            OnPropertyChanged(nameof(DescriptionText));
            PopulateForm(value);
        }
    }

    public bool IsEditing => _selectedCommand != null;

    public string PlaceholderHint
    {
        get
        {
            var info = _selectedCommand == null ? null : BuiltInCommandCatalog.Info(_selectedCommand.Id);
            return string.IsNullOrEmpty(info?.Placeholders) ? string.Empty : $"Placeholders: {info!.Placeholders}";
        }
    }

    public string DescriptionText => _selectedCommand == null ? string.Empty
        : BuiltInCommandCatalog.Info(_selectedCommand.Id)?.Description ?? string.Empty;

    private string _editKeyword = string.Empty;
    public string EditKeyword { get => _editKeyword; set => SetField(ref _editKeyword, value); }

    private string _editReply = string.Empty;
    public string EditReply { get => _editReply; set => SetField(ref _editReply, value); }

    private bool _allowMods, _allowSubs, _allowVips, _allowRegulars, _allowEveryone;
    public bool AllowModerators { get => _allowMods; set => SetField(ref _allowMods, value); }
    public bool AllowSubscribers { get => _allowSubs; set => SetField(ref _allowSubs, value); }
    public bool AllowVIPs { get => _allowVips; set => SetField(ref _allowVips, value); }
    public bool AllowRegulars { get => _allowRegulars; set => SetField(ref _allowRegulars, value); }
    public bool AllowEveryone { get => _allowEveryone; set => SetField(ref _allowEveryone, value); }

    public RelayCommand SaveCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand ToggleEnabledCommand { get; }

    public BuiltInCommandsViewModel(SettingsRepository settingsRepo)
    {
        _settingsRepo = settingsRepo;
        SaveCommand = new RelayCommand(Save, () => SelectedCommand != null && !string.IsNullOrWhiteSpace(EditKeyword));
        ResetCommand = new RelayCommand(Reset, () => SelectedCommand != null);
        ToggleEnabledCommand = new RelayCommand(_ => Persist());
        Refresh();
    }

    public void Refresh()
    {
        var selectedId = _selectedCommand?.Id;
        Commands.Clear();
        foreach (var c in BuiltInCommandCatalog.Resolve(_settingsRepo.GetSettings()))
            Commands.Add(c);
        SelectedCommand = selectedId != null ? Commands.FirstOrDefault(c => c.Id == selectedId) : null;
    }

    private void PopulateForm(BuiltInCommandConfig? cfg)
    {
        if (cfg == null)
        {
            EditKeyword = string.Empty;
            EditReply = string.Empty;
            AllowModerators = AllowSubscribers = AllowVIPs = AllowRegulars = AllowEveryone = false;
            return;
        }
        EditKeyword = cfg.Keyword;
        EditReply = cfg.Reply;
        AllowModerators = cfg.AllowedRoles.Contains(UserRoles.Moderator);
        AllowSubscribers = cfg.AllowedRoles.Contains(UserRoles.Subscriber);
        AllowVIPs = cfg.AllowedRoles.Contains(UserRoles.VIP);
        AllowRegulars = cfg.AllowedRoles.Contains(UserRoles.Regular);
        AllowEveryone = cfg.AllowedRoles.Contains(UserRoles.Everyone);
    }

    private List<string> BuildRoles()
    {
        var roles = new List<string>();
        if (AllowEveryone) roles.Add(UserRoles.Everyone);
        if (AllowModerators) roles.Add(UserRoles.Moderator);
        if (AllowSubscribers) roles.Add(UserRoles.Subscriber);
        if (AllowVIPs) roles.Add(UserRoles.VIP);
        if (AllowRegulars) roles.Add(UserRoles.Regular);
        return roles;
    }

    private void Save()
    {
        if (_selectedCommand == null || string.IsNullOrWhiteSpace(EditKeyword)) return;
        _selectedCommand.Keyword = EditKeyword.Trim();
        _selectedCommand.Reply = EditReply;
        _selectedCommand.AllowedRoles = BuildRoles();
        Persist();
    }

    private void Reset()
    {
        if (_selectedCommand == null) return;
        var info = BuiltInCommandCatalog.Info(_selectedCommand.Id);
        if (info == null) return;
        _selectedCommand.Keyword = info.DefaultKeyword;
        _selectedCommand.Reply = info.DefaultReply;
        _selectedCommand.AllowedRoles = info.DefaultRoles.ToList();
        _selectedCommand.Enabled = true;
        PopulateForm(_selectedCommand);
        Persist();
    }

    private void Persist()
    {
        _settingsRepo.Update(s => s.BuiltInCommands = Commands.ToList());
        Refresh();
    }
}
