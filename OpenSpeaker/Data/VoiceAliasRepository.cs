using OpenSpeaker.Models;
namespace OpenSpeaker.Data;

public class VoiceAliasRepository : LiteDbRepository<VoiceAlias>
{
    public VoiceAliasRepository(DatabaseContext db) : base(db.VoiceAliases) { }

    public VoiceAlias? GetByName(string name) =>
        _collection.FindOne(a => a.Name == name);

    public IEnumerable<VoiceAlias> GetAllSorted() =>
        _collection.FindAll().OrderBy(a => a.Name);
}
