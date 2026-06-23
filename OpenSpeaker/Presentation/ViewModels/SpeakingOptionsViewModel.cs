using System.Collections.ObjectModel;
using OpenSpeaker.Data;
using OpenSpeaker.Models;
using OpenSpeaker.Twitch;
namespace OpenSpeaker.ViewModels;

public class SpeakingOptionsViewModel : BaseViewModel
{
    private readonly SettingsRepository _settingsRepo;
    private readonly EmoteCacheService _emoteCache;
    private readonly ITwitchService _twitch;
    private AppSettings _settings;

    public bool SayUsername { get => _settings.SayUsername; set { _settings.SayUsername = value; OnPropertyChanged(); Save(); } }
    public string SayUsernamePrefix { get => _settings.SayUsernamePrefix; set { _settings.SayUsernamePrefix = value; OnPropertyChanged(); Save(); } }
    public bool StickyRandomVoice { get => _settings.StickyRandomVoice; set { _settings.StickyRandomVoice = value; OnPropertyChanged(); Save(); } }
    public bool OnlySayUsernameIfDifferent { get => _settings.OnlySayUsernameIfDifferent; set { _settings.OnlySayUsernameIfDifferent = value; OnPropertyChanged(); Save(); } }
    public bool ReplaceNameWithNickname { get => _settings.ReplaceNameWithNickname; set { _settings.ReplaceNameWithNickname = value; OnPropertyChanged(); Save(); } }

    public bool UseHighlightVoice { get => _settings.UseHighlightVoice; set { _settings.UseHighlightVoice = value; OnPropertyChanged(); Save(); } }
    public string HighlightVoiceAlias { get => _settings.HighlightVoiceAlias; set { _settings.HighlightVoiceAlias = value; OnPropertyChanged(); Save(); } }

    public bool StopOnMessageDeleted { get => _settings.StopOnMessageDeleted; set { _settings.StopOnMessageDeleted = value; OnPropertyChanged(); Save(); } }
    public bool SkipOnMessageDeleted { get => _settings.SkipOnMessageDeleted; set { _settings.SkipOnMessageDeleted = value; OnPropertyChanged(); Save(); } }
    public bool StopOnUserTimedOut { get => _settings.StopOnUserTimedOut; set { _settings.StopOnUserTimedOut = value; OnPropertyChanged(); Save(); } }
    public bool SkipOnUserTimedOut { get => _settings.SkipOnUserTimedOut; set { _settings.SkipOnUserTimedOut = value; OnPropertyChanged(); Save(); } }
    public bool StopOnUserBanned { get => _settings.StopOnUserBanned; set { _settings.StopOnUserBanned = value; OnPropertyChanged(); Save(); } }
    public bool SkipOnUserBanned { get => _settings.SkipOnUserBanned; set { _settings.SkipOnUserBanned = value; OnPropertyChanged(); Save(); } }
    public bool SilenceCommandOutput { get => _settings.SilenceCommandOutput; set { _settings.SilenceCommandOutput = value; OnPropertyChanged(); Save(); } }

    public bool AllowModerators { get => _settings.AllowModerators; set { _settings.AllowModerators = value; OnPropertyChanged(); Save(); } }
    public bool AllowSubscribers { get => _settings.AllowSubscribers; set { _settings.AllowSubscribers = value; OnPropertyChanged(); Save(); } }
    public bool AllowVIPs { get => _settings.AllowVIPs; set { _settings.AllowVIPs = value; OnPropertyChanged(); Save(); } }
    public bool AllowRegulars { get => _settings.AllowRegulars; set { _settings.AllowRegulars = value; OnPropertyChanged(); Save(); } }
    public bool AllowEveryone { get => _settings.AllowEveryone; set { _settings.AllowEveryone = value; OnPropertyChanged(); Save(); } }

    public bool StripTwitchEmotes { get => _settings.StripTwitchEmotes; set { _settings.StripTwitchEmotes = value; OnPropertyChanged(); Save(); } }
    public bool StripBttvEmotes { get => _settings.StripBttvEmotes; set { _settings.StripBttvEmotes = value; OnPropertyChanged(); Save(); } }
    public bool StripFfzEmotes { get => _settings.StripFfzEmotes; set { _settings.StripFfzEmotes = value; OnPropertyChanged(); Save(); } }
    public bool StripSevenTvEmotes { get => _settings.StripSevenTvEmotes; set { _settings.StripSevenTvEmotes = value; OnPropertyChanged(); Save(); } }
    public bool StripCheermotes { get => _settings.StripCheermotes; set { _settings.StripCheermotes = value; OnPropertyChanged(); Save(); } }
    public bool StripTwemoji { get => _settings.StripTwemoji; set { _settings.StripTwemoji = value; OnPropertyChanged(); Save(); } }
    public bool AllowFirstEmote { get => _settings.AllowFirstEmote; set { _settings.AllowFirstEmote = value; OnPropertyChanged(); Save(); } }

    public int CooldownSeconds { get => _settings.CooldownSeconds; set { _settings.CooldownSeconds = value; OnPropertyChanged(); Save(); } }
    public int MaxWords { get => _settings.MaxWords; set { _settings.MaxWords = value; OnPropertyChanged(); Save(); } }
    public int MaxChars { get => _settings.MaxChars; set { _settings.MaxChars = value; OnPropertyChanged(); Save(); } }
    public bool WordLimitSymbolsAsSpaces { get => _settings.WordLimitSymbolsAsSpaces; set { _settings.WordLimitSymbolsAsSpaces = value; OnPropertyChanged(); Save(); } }
    public string NotAllowedText { get => _settings.NotAllowedText; set { _settings.NotAllowedText = value; OnPropertyChanged(); Save(); } }
    public string UrlFilterMode { get => _settings.UrlFilterMode; set { _settings.UrlFilterMode = value; OnPropertyChanged(); Save(); } }

    public IEnumerable<string> UrlFilterModes { get; } = new[] { "Disabled", "Strip", "Block" };

    public string Mode { get => _settings.Mode; set { _settings.Mode = value; OnPropertyChanged(); Save(); } }
    public IEnumerable<string> AvailableModes { get; } = new[] { TtsModes.Everything, TtsModes.Command };

    public ObservableCollection<string> AllowedEmotes { get; } = new();
    public ObservableCollection<string> TtsCommands { get; } = new();
    public ObservableCollection<string> IgnoredPrefixes { get; } = new();

    private string _newAllowedEmote = string.Empty;
    public string NewAllowedEmote { get => _newAllowedEmote; set => SetField(ref _newAllowedEmote, value); }

    private string _newTtsCommand = string.Empty;
    public string NewTtsCommand { get => _newTtsCommand; set => SetField(ref _newTtsCommand, value); }

    private string _newIgnoredPrefix = string.Empty;
    public string NewIgnoredPrefix { get => _newIgnoredPrefix; set => SetField(ref _newIgnoredPrefix, value); }

    private string? _selectedAllowedEmote;
    public string? SelectedAllowedEmote { get => _selectedAllowedEmote; set => SetField(ref _selectedAllowedEmote, value); }

    private string? _selectedTtsCommand;
    public string? SelectedTtsCommand { get => _selectedTtsCommand; set => SetField(ref _selectedTtsCommand, value); }

    private string? _selectedIgnoredPrefix;
    public string? SelectedIgnoredPrefix { get => _selectedIgnoredPrefix; set => SetField(ref _selectedIgnoredPrefix, value); }

    public RelayCommand RefreshEmotesCommand { get; }
    public RelayCommand AddEmoteCommand { get; }
    public RelayCommand DeleteEmoteCommand { get; }
    public RelayCommand AddTtsCommandCommand { get; }
    public RelayCommand DeleteTtsCommandCommand { get; }
    public RelayCommand AddIgnoredPrefixCommand { get; }
    public RelayCommand DeleteIgnoredPrefixCommand { get; }

    public SpeakingOptionsViewModel(SettingsRepository settingsRepo, EmoteCacheService emoteCache, ITwitchService twitch)
    {
        _settingsRepo = settingsRepo;
        _emoteCache = emoteCache;
        _twitch = twitch;
        _settings = settingsRepo.GetSettings();

        foreach (var e in _settings.AllowedEmotes) AllowedEmotes.Add(e);
        foreach (var c in _settings.TtsCommands) TtsCommands.Add(c);
        foreach (var p in _settings.IgnoredPrefixes) IgnoredPrefixes.Add(p);

        RefreshEmotesCommand = new RelayCommand(RefreshEmotes);
        AddEmoteCommand = new RelayCommand(AddEmote, () => !string.IsNullOrWhiteSpace(NewAllowedEmote));
        DeleteEmoteCommand = new RelayCommand(DeleteEmote, () => SelectedAllowedEmote != null);
        AddTtsCommandCommand = new RelayCommand(AddTtsCommand, () => !string.IsNullOrWhiteSpace(NewTtsCommand));
        DeleteTtsCommandCommand = new RelayCommand(DeleteTtsCommand, () => SelectedTtsCommand != null);
        AddIgnoredPrefixCommand = new RelayCommand(AddIgnoredPrefix, () => !string.IsNullOrWhiteSpace(NewIgnoredPrefix));
        DeleteIgnoredPrefixCommand = new RelayCommand(DeleteIgnoredPrefix, () => SelectedIgnoredPrefix != null);
    }

    private async void RefreshEmotes()
    {
        var id = _twitch.BroadcasterId;
        if (!string.IsNullOrEmpty(id))
            await _emoteCache.RefreshAsync(id);
    }

    public void Refresh()
    {
        _settings = _settingsRepo.GetSettings();
        AllowedEmotes.Clear();
        foreach (var e in _settings.AllowedEmotes) AllowedEmotes.Add(e);
        TtsCommands.Clear();
        foreach (var c in _settings.TtsCommands) TtsCommands.Add(c);
        IgnoredPrefixes.Clear();
        foreach (var p in _settings.IgnoredPrefixes) IgnoredPrefixes.Add(p);
        OnPropertyChanged(string.Empty);
    }

    private void Save()
    {
        _settings.AllowedEmotes = AllowedEmotes.ToList();
        _settings.TtsCommands = TtsCommands.ToList();
        _settings.IgnoredPrefixes = IgnoredPrefixes.ToList();
        _settingsRepo.SaveSettings(_settings);
    }

    private void AddEmote() { if (!string.IsNullOrWhiteSpace(NewAllowedEmote)) { AllowedEmotes.Add(NewAllowedEmote); NewAllowedEmote = string.Empty; Save(); } }
    private void DeleteEmote() { if (SelectedAllowedEmote != null) { AllowedEmotes.Remove(SelectedAllowedEmote); SelectedAllowedEmote = null; Save(); } }
    private void AddTtsCommand() { if (!string.IsNullOrWhiteSpace(NewTtsCommand)) { TtsCommands.Add(NewTtsCommand); NewTtsCommand = string.Empty; Save(); } }
    private void DeleteTtsCommand() { if (SelectedTtsCommand != null) { TtsCommands.Remove(SelectedTtsCommand); SelectedTtsCommand = null; Save(); } }
    private void AddIgnoredPrefix() { if (!string.IsNullOrWhiteSpace(NewIgnoredPrefix)) { IgnoredPrefixes.Add(NewIgnoredPrefix); NewIgnoredPrefix = string.Empty; Save(); } }
    private void DeleteIgnoredPrefix() { if (SelectedIgnoredPrefix != null) { IgnoredPrefixes.Remove(SelectedIgnoredPrefix); SelectedIgnoredPrefix = null; Save(); } }
}
