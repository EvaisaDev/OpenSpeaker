using LiteDB;
using OpenSpeaker.Models;
namespace OpenSpeaker.Data;

public class WordDictionaryRepository
{
	private readonly DatabaseContext _db;
	private volatile List<WordDictionaryEntry>? _cache;

	public WordDictionaryRepository(DatabaseContext db) { _db = db; }

	public List<WordDictionaryEntry> GetAll() =>
		_cache ??= _db.WordDictionary.FindAll().ToList();

	public void Insert(WordDictionaryEntry e)
	{
		_db.WordDictionary.Insert(e);
		_cache = null;
	}

	public void Upsert(WordDictionaryEntry e)
	{
		_db.WordDictionary.Upsert(e);
		_cache = null;
	}

	public void Delete(ObjectId id)
	{
		_db.WordDictionary.Delete(id);
		_cache = null;
	}

	public void Invalidate() => _cache = null;
}
