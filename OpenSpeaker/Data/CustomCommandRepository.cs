using LiteDB;
using OpenSpeaker.Models;
namespace OpenSpeaker.Data;

public class CustomCommandRepository
{
    private readonly DatabaseContext _db;
    private volatile List<CustomCommand>? _cache;

    public CustomCommandRepository(DatabaseContext db) { _db = db; }

    public List<CustomCommand> GetAll() =>
        _cache ??= _db.CustomCommands.FindAll().OrderBy(c => c.Trigger).ToList();

    public void Upsert(CustomCommand cmd)
    {
        _db.CustomCommands.Upsert(cmd);
        _cache = null;
    }

    public void Delete(ObjectId id)
    {
        _db.CustomCommands.Delete(id);
        _cache = null;
    }

    public void Invalidate() => _cache = null;
}
