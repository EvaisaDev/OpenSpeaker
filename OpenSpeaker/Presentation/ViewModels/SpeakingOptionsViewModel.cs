using System.Collections.ObjectModel;
using OpenSpeaker.Data;
using OpenSpeaker.Models;
using OpenSpeaker.Twitch;
namespace OpenSpeaker.ViewModels;

public class SpeakingOptionsViewModel : SettingsViewModelBase
{
    private readonly EmoteCacheService _emoteCache;
    private readonly ITwitchService _twitch;
    private readonly VoiceAliasRepository _aliasRepo;

    public ObservableCollection<string> AvailableAliases { get; } = new();

    public bool SayUsername { get => Settings.SayUsername; set => Set(s => s.SayUsername = value); }
    public string SayUsernamePrefix { get => Settings.SayUsernamePrefix; set => Set(s => s.SayUsernamePrefix = value); }
    public bool StickyRandomVoice { get => Settings.StickyRandomVoice; set => Set(s => s.StickyRandomVoice = value); }
    public bool OnlySayUsernameIfDifferent { get => Settings.OnlySayUsernameIfDifferent; set => Set(s => s.OnlySayUsernameIfDifferent = value); }
    public bool ReplaceNameWithNickname { get => Settings.ReplaceNameWithNickname; set => Set(s => s.ReplaceNameWithNickname = value); }

    public bool UseHighlightVoice { get => Settings.UseHighlightVoice; set => Set(s => s.UseHighlightVoice = value); }
    public string HighlightVoiceAlias
    {
        get => string.IsNullOrEmpty(Settings.HighlightVoiceAlias) ? NoneAlias : Settings.HighlightVoiceAlias;
        set => Set(s => s.HighlightVoiceAlias = value == NoneAlias ? string.Empty : value);
    }

    public bool StopOnMessageDeleted { get => Settings.StopOnMessageDeleted; set => Set(s => s.StopOnMessageDeleted = value); }
    public bool SkipOnMessageDeleted { get => Settings.SkipOnMessageDeleted; set => Set(s => s.SkipOnMessageDeleted = value); }
    public bool StopOnUserTimedOut { get => Settings.StopOnUserTimedOut; set => Set(s => s.StopOnUserTimedOut = value); }
    public bool SkipOnUserTimedOut { get => Settings.SkipOnUserTimedOut; set => Set(s => s.SkipOnUserTimedOut = value); }
    public bool StopOnUserBanned { get => Settings.StopOnUserBanned; set => Set(s => s.StopOnUserBanned = value); }
    public bool SkipOnUserBanned { get => Settings.SkipOnUserBanned; set => Set(s => s.SkipOnUserBanned = value); }
    public bool SilenceCommandOutput { get => Settings.SilenceCommandOutput; set => Set(s => s.SilenceCommandOutput = value); }

    public bool AllowModerators { get => Settings.AllowModerators; set => Set(s => s.AllowModerators = value); }
    public bool AllowSubscribers { get => Settings.AllowSubscribers; set => Set(s => s.AllowSubscribers = value); }
    public bool AllowVIPs { get => Settings.AllowVIPs; set => Set(s => s.AllowVIPs = value); }
    public bool AllowRegulars { get => Settings.AllowRegulars; set => Set(s => s.AllowRegulars = value); }
    public bool AllowEveryone { get => Settings.AllowEveryone; set => Set(s => s.AllowEveryone = value); }

    public bool StripTwitchEmotes { get => Settings.StripTwitchEmotes; set => Set(s => s.StripTwitchEmotes = value); }
    public bool StripBttvEmotes { get => Settings.StripBttvEmotes; set => Set(s => s.StripBttvEmotes = value); }
    public bool StripFfzEmotes { get => Settings.StripFfzEmotes; set => Set(s => s.StripFfzEmotes = value); }
    public bool StripSevenTvEmotes { get => Settings.StripSevenTvEmotes; set => Set(s => s.StripSevenTvEmotes = value); }
    public bool StripCheermotes { get => Settings.StripCheermotes; set => Set(s => s.StripCheermotes = value); }
    public bool StripTwemoji { get => Settings.StripTwemoji; set => Set(s => s.StripTwemoji = value); }
    public bool AllowFirstEmote { get => Settings.AllowFirstEmote; set => Set(s => s.AllowFirstEmote = value); }

    public int CooldownSeconds { get => Settings.CooldownSeconds; set => Set(s => s.CooldownSeconds = value); }
    public int MaxWords { get => Settings.MaxWords; set => Set(s => s.MaxWords = value); }
    public int MaxChars { get => Settings.MaxChars; set => Set(s => s.MaxChars = value); }
    public bool WordLimitSymbolsAsSpaces { get => Settings.WordLimitSymbolsAsSpaces; set => Set(s => s.WordLimitSymbolsAsSpaces = value); }
    public string NotAllowedText { get => Settings.NotAllowedText; set => Set(s => s.NotAllowedText = value); }
    public string UrlFilterMode { get => Settings.UrlFilterMode; set => Set(s => s.UrlFilterMode = value); }

    public IEnumerable<string> UrlFilterModes { get; } = new[] { "Disabled", "Strip", "Block" };

    public string Mode { get => Settings.Mode; set => Set(s => s.Mode = value); }
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

    public SpeakingOptionsViewModel(SettingsRepository settingsRepo, EmoteCacheService emoteCache, ITwitchService twitch, VoiceAliasRepository aliasRepo)
        : base(settingsRepo)
    {
        _emoteCache = emoteCache;
        _twitch = twitch;
        _aliasRepo = aliasRepo;
        RefreshAliases();

        foreach (var e in Settings.AllowedEmotes) AllowedEmotes.Add(e);
        foreach (var c in Settings.TtsCommands) TtsCommands.Add(c);
        foreach (var p in Settings.IgnoredPrefixes) IgnoredPrefixes.Add(p);

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

    public void RefreshAliases()
    {
        AvailableAliases.Clear();
        AvailableAliases.Add(NoneAlias);
        foreach (var a in _aliasRepo.GetAllSorted())
            AvailableAliases.Add(a.Name);
    }

    public override void Refresh()
    {
        Settings = SettingsRepo.GetSettings();
        AllowedEmotes.Clear();
        foreach (var e in Settings.AllowedEmotes) AllowedEmotes.Add(e);
        TtsCommands.Clear();
        foreach (var c in Settings.TtsCommands) TtsCommands.Add(c);
        IgnoredPrefixes.Clear();
        foreach (var p in Settings.IgnoredPrefixes) IgnoredPrefixes.Add(p);
        OnPropertyChanged(string.Empty);
    }

    protected override void Persist()
    {
        Settings.AllowedEmotes = AllowedEmotes.ToList();
        Settings.TtsCommands = TtsCommands.ToList();
        Settings.IgnoredPrefixes = IgnoredPrefixes.ToList();
        base.Persist();
    }

    private void AddEmote() { if (!string.IsNullOrWhiteSpace(NewAllowedEmote)) { AllowedEmotes.Add(NewAllowedEmote); NewAllowedEmote = string.Empty; Persist(); } }
    private void DeleteEmote() { if (SelectedAllowedEmote != null) { AllowedEmotes.Remove(SelectedAllowedEmote); SelectedAllowedEmote = null; Persist(); } }
    private void AddTtsCommand() { if (!string.IsNullOrWhiteSpace(NewTtsCommand)) { TtsCommands.Add(NewTtsCommand); NewTtsCommand = string.Empty; Persist(); } }
    private void DeleteTtsCommand() { if (SelectedTtsCommand != null) { TtsCommands.Remove(SelectedTtsCommand); SelectedTtsCommand = null; Persist(); } }
    private void AddIgnoredPrefix() { if (!string.IsNullOrWhiteSpace(NewIgnoredPrefix)) { IgnoredPrefixes.Add(NewIgnoredPrefix); NewIgnoredPrefix = string.Empty; Persist(); } }
    private void DeleteIgnoredPrefix() { if (SelectedIgnoredPrefix != null) { IgnoredPrefixes.Remove(SelectedIgnoredPrefix); SelectedIgnoredPrefix = null; Persist(); } }
}
