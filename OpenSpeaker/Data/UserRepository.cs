using LiteDB;
using OpenSpeaker.Models;
namespace OpenSpeaker.Data;

public class UserRepository : LiteDbRepository<UserRecord>
{
    public UserRepository(DatabaseContext db) : base(db.Users) { }

    public UserRecord? FindByTwitchId(string twitchId) =>
        _collection.FindOne(u => u.TwitchId == twitchId);

    public UserRecord? FindByUsername(string username)
    {
        var name = username.TrimStart('@').ToLowerInvariant();
        return _collection.FindOne(u => u.Username.ToLower() == name);
    }

    public List<UserRecord> GetIgnored() =>
        _collection.Find(u => u.IsIgnored).ToList();
}
