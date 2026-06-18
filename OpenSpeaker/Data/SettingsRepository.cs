using OpenSpeaker.Models;
namespace OpenSpeaker.Data;

public class SettingsRepository
{
    private readonly DatabaseContext _db;

    public SettingsRepository(DatabaseContext db)
    {
        _db = db;
    }

    public AppSettings GetSettings()
    {
        return _db.Settings.FindById(1) ?? new AppSettings();
    }

    public void SaveSettings(AppSettings settings)
    {
        settings.Id = 1;
        _db.Settings.Upsert(settings);
    }
}
