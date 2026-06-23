using OpenSpeaker.Models;
namespace OpenSpeaker.Data;

public class SettingsRepository
{
    private readonly DatabaseContext _db;
    private readonly object _lock = new();
    private AppSettings? _cached;

    public SettingsRepository(DatabaseContext db)
    {
        _db = db;
    }

    public AppSettings GetSettings()
    {
        lock (_lock)
            return _cached ??= (_db.Settings.FindById(1) ?? new AppSettings());
    }

    public void SaveSettings(AppSettings settings)
    {
        lock (_lock)
        {
            settings.Id = 1;
            _cached = settings;
            _db.Settings.Upsert(settings);
        }
    }

    public void Update(Action<AppSettings> mutate)
    {
        lock (_lock)
        {
            var settings = _cached ??= (_db.Settings.FindById(1) ?? new AppSettings());
            mutate(settings);
            settings.Id = 1;
            _db.Settings.Upsert(settings);
        }
    }

    public void Invalidate()
    {
        lock (_lock)
            _cached = null;
    }
}
