using LiteDB;
using OpenSpeaker.Models;
namespace OpenSpeaker.Data;

public class ChannelRewardRepository
{
    private readonly DatabaseContext _db;
    public ChannelRewardRepository(DatabaseContext db) { _db = db; }

    public List<ChannelReward> GetAll() => _db.ChannelRewards.FindAll().OrderBy(r => r.Title).ToList();
    public ChannelReward? GetByTwitchId(string twitchId) => _db.ChannelRewards.FindOne(r => r.TwitchRewardId == twitchId);
    public void Upsert(ChannelReward reward) => _db.ChannelRewards.Upsert(reward);
    public void Delete(ObjectId id) => _db.ChannelRewards.Delete(id);
    public void UpsertMany(IEnumerable<ChannelReward> rewards)
    {
        foreach (var r in rewards)
            _db.ChannelRewards.Upsert(r);
    }
}
