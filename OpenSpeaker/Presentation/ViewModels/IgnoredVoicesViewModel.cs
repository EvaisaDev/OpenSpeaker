using System.Collections.ObjectModel;
using System.Windows.Input;
using LiteDB;
using OpenSpeaker.Data;
using OpenSpeaker.Models;
using OpenSpeaker.Services;
using OpenSpeaker.TTS;
namespace OpenSpeaker.ViewModels;

public class IgnoredVoicesViewModel : BaseViewModel
{
    private readonly DatabaseContext _db;
    private readonly TtsEngineRegistry _registry;
    private readonly VoicePool _voicePool;

    public ObservableCollection<string> AvailableVoices { get; } = new();
    public ObservableCollection<string> IgnoredVoices { get; } = new();
    public ObservableCollection<string> AvailableLocales { get; } = new();
    public ObservableCollection<string> IgnoredLocales { get; } = new();
    public ObservableCollection<IgnoreProfile> Profiles { get; } = new();

    private string? _selectedAvailableVoice;
    public string? SelectedAvailableVoice { get => _selectedAvailableVoice; set => SetField(ref _selectedAvailableVoice, value); }

    private string? _selectedIgnoredVoice;
    public string? SelectedIgnoredVoice { get => _selectedIgnoredVoice; set => SetField(ref _selectedIgnoredVoice, value); }

    private string? _selectedAvailableLocale;
    public string? SelectedAvailableLocale { get => _selectedAvailableLocale; set => SetField(ref _selectedAvailableLocale, value); }

    private string? _selectedIgnoredLocale;
    public string? SelectedIgnoredLocale { get => _selectedIgnoredLocale; set => SetField(ref _selectedIgnoredLocale, value); }

    private IgnoreProfile? _selectedProfile;
    public IgnoreProfile? SelectedProfile
    {
        get => _selectedProfile;
        set { SetField(ref _selectedProfile, value); ActivateProfile(value); LoadProfile(); }
    }

    private string _newProfileName = string.Empty;
    public string NewProfileName
    {
        get => _newProfileName;
        set { SetField(ref _newProfileName, value); CommandManager.InvalidateRequerySuggested(); }
    }

    public RelayCommand IgnoreVoiceCommand { get; }
    public RelayCommand UnignoreVoiceCommand { get; }
    public RelayCommand IgnoreLocaleCommand { get; }
    public RelayCommand UnignoreLocaleCommand { get; }
    public RelayCommand NewProfileCommand { get; }
    public RelayCommand DeleteProfileCommand { get; }

    private readonly List<string> _allVoices = new();
    private readonly List<string> _allLocales = new();

    public IgnoredVoicesViewModel(DatabaseContext db, TtsEngineRegistry registry, VoicePool voicePool)
    {
        _db = db;
        _registry = registry;
        _voicePool = voicePool;

        IgnoreVoiceCommand = new RelayCommand(IgnoreVoice, () => SelectedAvailableVoice != null);
        UnignoreVoiceCommand = new RelayCommand(UnignoreVoice, () => SelectedIgnoredVoice != null);
        IgnoreLocaleCommand = new RelayCommand(IgnoreLocale, () => SelectedAvailableLocale != null);
        UnignoreLocaleCommand = new RelayCommand(UnignoreLocale, () => SelectedIgnoredLocale != null);
        NewProfileCommand = new RelayCommand(NewProfile, () => !string.IsNullOrWhiteSpace(NewProfileName));
        DeleteProfileCommand = new RelayCommand(DeleteProfile, () => SelectedProfile != null);

        _ = LoadAllVoicesAsync();
        RefreshProfiles();
    }

    private async Task LoadAllVoicesAsync()
    {
        var all = await _voicePool.GetAllAsync();
        foreach (var (engineId, voice) in all)
        {
            _allVoices.Add($"{engineId}::{voice.Id}");
            if (!string.IsNullOrEmpty(voice.Locale) && !_allLocales.Contains(voice.Locale))
                _allLocales.Add(voice.Locale);
        }
        RefreshLists();
    }

    private void RefreshLists()
    {
        var ignored = _selectedProfile?.ExcludedVoiceIds ?? new List<string>();
        var ignoredLocales = _selectedProfile?.ExcludedLocales ?? new List<string>();

        AvailableVoices.Clear();
        foreach (var v in _allVoices.Where(v => !ignored.Contains(v)))
            AvailableVoices.Add(v);

        IgnoredVoices.Clear();
        foreach (var v in ignored)
            IgnoredVoices.Add(v);

        AvailableLocales.Clear();
        foreach (var l in _allLocales.Where(l => !ignoredLocales.Contains(l)))
            AvailableLocales.Add(l);

        IgnoredLocales.Clear();
        foreach (var l in ignoredLocales)
            IgnoredLocales.Add(l);
    }

    private void ActivateProfile(IgnoreProfile? profile)
    {
        foreach (var p in _db.IgnoreProfiles.FindAll())
        {
            var wasActive = p.IsActive;
            p.IsActive = profile != null && p.Id == profile.Id;
            if (p.IsActive != wasActive)
                _db.IgnoreProfiles.Update(p);
        }
    }

    private void LoadProfile()
    {
        RefreshLists();
    }

    public void Refresh()
    {
        RefreshProfiles();
        SelectedProfile = Profiles.FirstOrDefault(p => p.IsActive) ?? Profiles.FirstOrDefault();
    }

    private void RefreshProfiles()
    {
        Profiles.Clear();
        foreach (var p in _db.IgnoreProfiles.FindAll())
            Profiles.Add(p);
    }

    private void IgnoreVoice()
    {
        if (_selectedAvailableVoice == null) return;
        EnsureActiveProfile();
        if (_selectedProfile == null) return;
        if (!_selectedProfile.ExcludedVoiceIds.Contains(_selectedAvailableVoice))
            _selectedProfile.ExcludedVoiceIds.Add(_selectedAvailableVoice);
        Save();
        RefreshLists();
    }

    private void UnignoreVoice()
    {
        if (_selectedIgnoredVoice == null || _selectedProfile == null) return;
        _selectedProfile.ExcludedVoiceIds.Remove(_selectedIgnoredVoice);
        Save();
        RefreshLists();
    }

    private void IgnoreLocale()
    {
        if (_selectedAvailableLocale == null) return;
        EnsureActiveProfile();
        if (_selectedProfile == null) return;
        if (!_selectedProfile.ExcludedLocales.Contains(_selectedAvailableLocale))
            _selectedProfile.ExcludedLocales.Add(_selectedAvailableLocale);
        Save();
        RefreshLists();
    }

    private void UnignoreLocale()
    {
        if (_selectedIgnoredLocale == null || _selectedProfile == null) return;
        _selectedProfile.ExcludedLocales.Remove(_selectedIgnoredLocale);
        Save();
        RefreshLists();
    }

    private void EnsureActiveProfile()
    {
        if (_selectedProfile == null)
        {
            var active = new IgnoreProfile { Name = "Default", IsActive = true };
            _db.IgnoreProfiles.Insert(active);
            RefreshProfiles();
            _selectedProfile = Profiles.FirstOrDefault();
        }
    }

    private void NewProfile()
    {
        var p = new IgnoreProfile { Name = NewProfileName };
        _db.IgnoreProfiles.Insert(p);
        RefreshProfiles();
        SelectedProfile = Profiles.FirstOrDefault(x => x.Name == NewProfileName);
        NewProfileName = string.Empty;
    }

    private void DeleteProfile()
    {
        if (_selectedProfile == null) return;
        _db.IgnoreProfiles.Delete(_selectedProfile.Id);
        RefreshProfiles();
        SelectedProfile = Profiles.FirstOrDefault();
    }

    private void Save()
    {
        if (_selectedProfile == null) return;
        _db.IgnoreProfiles.Upsert(_selectedProfile);
    }
}
