using LiteDB;
using OpenSpeaker.Models;
namespace OpenSpeaker.Data;

public class UserRepository : LiteDbRepository<UserRecord>
{
    public UserRepository(DatabaseContext db) : base(db.Users) { }

    public UserRecord? FindByTwitchId(string twitchId) =>
        _collection.FindOne(u => u.TwitchId == twitchId);

    public UserRecord? FindByUsername(string username) =>
        _collection.FindOne(u => u.Username.ToLower() == username.ToLower());

    public List<UserRecord> GetIgnored() =>
        _collection.Find(u => u.IsIgnored).ToList();

    public void Upsert(UserRecord user) => _collection.Upsert(user);
}
