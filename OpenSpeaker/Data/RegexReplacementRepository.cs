using LiteDB;
using OpenSpeaker.Models;
namespace OpenSpeaker.Data;

public class RegexReplacementRepository
{
    private readonly DatabaseContext _db;
    private volatile List<RegexReplacement>? _cache;

    public RegexReplacementRepository(DatabaseContext db) { _db = db; }

    public List<RegexReplacement> GetAll() =>
        _cache ??= _db.RegexReplacements.FindAll().ToList();

    public int Count() => _db.RegexReplacements.Count();

    public void Insert(RegexReplacement r)
    {
        _db.RegexReplacements.Insert(r);
        _cache = null;
    }

    public void Upsert(RegexReplacement r)
    {
        _db.RegexReplacements.Upsert(r);
        _cache = null;
    }

    public void Delete(ObjectId id)
    {
        _db.RegexReplacements.Delete(id);
        _cache = null;
    }

    public void Invalidate() => _cache = null;
}
