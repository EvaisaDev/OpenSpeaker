using OpenSpeaker.Models;
namespace OpenSpeaker.Data;

public class SettingsRepository
{
    private readonly DatabaseContext _db;
    private AppSettings? _cached;

    public SettingsRepository(DatabaseContext db)
    {
        _db = db;
    }

    public AppSettings GetSettings()
    {
        return _cached ??= (_db.Settings.FindById(1) ?? new AppSettings());
    }

    public void SaveSettings(AppSettings settings)
    {
        settings.Id = 1;
        _cached = settings;
        _db.Settings.Upsert(settings);
    }

    public void Invalidate() => _cached = null;
}
