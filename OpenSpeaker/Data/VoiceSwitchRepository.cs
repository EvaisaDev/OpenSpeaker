using LiteDB;
using OpenSpeaker.Models;
namespace OpenSpeaker.Data;

public class VoiceSwitchRepository
{
	private readonly DatabaseContext _db;
	private volatile List<VoiceSwitch>? _cache;

	public VoiceSwitchRepository(DatabaseContext db) { _db = db; }

	public List<VoiceSwitch> GetAll() =>
		_cache ??= _db.VoiceSwitches.FindAll().ToList();

	public void Insert(VoiceSwitch s)
	{
		_db.VoiceSwitches.Insert(s);
		_cache = null;
	}

	public void Upsert(VoiceSwitch s)
	{
		_db.VoiceSwitches.Upsert(s);
		_cache = null;
	}

	public void Delete(ObjectId id)
	{
		_db.VoiceSwitches.Delete(id);
		_cache = null;
	}

	public void Invalidate() => _cache = null;
}
