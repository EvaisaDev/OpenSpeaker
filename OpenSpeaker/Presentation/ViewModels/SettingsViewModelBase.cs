using System.Runtime.CompilerServices;
using OpenSpeaker.Data;
using OpenSpeaker.Models;
namespace OpenSpeaker.ViewModels;

public abstract class SettingsViewModelBase : BaseViewModel
{
    protected const string NoneAlias = "<None>";

    protected readonly SettingsRepository SettingsRepo;
    protected AppSettings Settings;

    protected SettingsViewModelBase(SettingsRepository settingsRepo)
    {
        SettingsRepo = settingsRepo;
        Settings = settingsRepo.GetSettings();
    }

    protected void Set(Action<AppSettings> apply, [CallerMemberName] string? name = null)
    {
        apply(Settings);
        OnPropertyChanged(name);
        Persist();
    }

    protected virtual void Persist() => SettingsRepo.SaveSettings(Settings);

    public virtual void Refresh()
    {
        Settings = SettingsRepo.GetSettings();
        OnPropertyChanged(string.Empty);
    }
}
