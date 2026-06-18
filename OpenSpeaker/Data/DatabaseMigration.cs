using LiteDB;
namespace OpenSpeaker.Data;

public class DatabaseMigration
{
    private readonly DatabaseContext _db;

    public DatabaseMigration(DatabaseContext db)
    {
        _db = db;
    }

    public void Run()
    {
        var eventRepo = new EventConfigRepository(_db);
        eventRepo.EnsureAllEventTypes();
    }
}
