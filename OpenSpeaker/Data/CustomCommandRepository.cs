using LiteDB;
using OpenSpeaker.Models;
namespace OpenSpeaker.Data;

public class CustomCommandRepository
{
    private readonly DatabaseContext _db;
    public CustomCommandRepository(DatabaseContext db) { _db = db; }

    public List<CustomCommand> GetAll() => _db.CustomCommands.FindAll().OrderBy(c => c.Trigger).ToList();
    public void Upsert(CustomCommand cmd) => _db.CustomCommands.Upsert(cmd);
    public void Delete(ObjectId id) => _db.CustomCommands.Delete(id);
}
