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
        MigrateVoiceGateProfiles();
        MigrateSimultaneousMode();
    }

    private void MigrateSimultaneousMode()
    {
        var col = _db.RawCollection("appsettings");
        var doc = col.FindOne(Query.All());
        if (doc == null) return;
        if (doc["SimultaneousMode"].AsBoolean && doc["QueueMode"].AsString == OpenSpeaker.Models.QueueModes.Sequential)
        {
            doc["QueueMode"] = OpenSpeaker.Models.QueueModes.Simultaneous;
            col.Update(doc);
        }
    }

    private void MigrateVoiceGateProfiles()
    {
        var col = _db.RawCollection("voicegateprofiles");
        var first = col.FindOne(Query.All());
        if (first != null && first["_id"].Type != BsonType.Guid)
            _db.DropCollection("voicegateprofiles");
    }
}
