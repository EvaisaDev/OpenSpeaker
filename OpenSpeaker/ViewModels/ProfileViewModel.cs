using System.Collections.ObjectModel;
using OpenSpeaker.Services;
namespace OpenSpeaker.ViewModels;

public class ProfileViewModel : BaseViewModel
{
    public const string CreateSentinel = "Create Profile...";

    private readonly ProfileService _profileService;
    public Action<string>? OnSwitch { get; set; }

    public ObservableCollection<string> Profiles { get; } = new();

    private string _selectedProfile = string.Empty;
    public string SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetField(ref _selectedProfile, value) && !string.IsNullOrEmpty(value))
                OnSwitch?.Invoke(value);
        }
    }

    public ProfileViewModel(ProfileService profileService)
    {
        _profileService = profileService;
        Reload();
    }

    public void Reload()
    {
        var manifest = _profileService.Load();
        Profiles.Clear();
        foreach (var p in manifest.Profiles) Profiles.Add(p);
        Profiles.Add(CreateSentinel);
        _selectedProfile = manifest.ActiveProfile;
        OnPropertyChanged(nameof(SelectedProfile));
    }

    public bool CreateProfile(string name)
    {
        if (!_profileService.AddProfile(name)) return false;
        Profiles.Insert(Profiles.Count - 1, name);
        SelectedProfile = name;
        return true;
    }
}
