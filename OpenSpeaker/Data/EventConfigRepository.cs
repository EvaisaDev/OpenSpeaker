using OpenSpeaker.Models;
namespace OpenSpeaker.Data;

public class EventConfigRepository : LiteDbRepository<EventConfig>
{
    public EventConfigRepository(DatabaseContext db) : base(db.Events) { }

    public EventConfig? GetByEventType(string eventType) =>
        _collection.FindOne(e => e.EventType == eventType);

    public void EnsureAllEventTypes()
    {
        var allTypes = new[]
        {
            EventTypes.Follow, EventTypes.Sub, EventTypes.Resub, EventTypes.GiftSub,
            EventTypes.GiftBomb, EventTypes.Cheer, EventTypes.Raid, EventTypes.ChannelPoint,
            EventTypes.HypeTrain, EventTypes.Goal, EventTypes.ChatMessage
        };

        foreach (var type in allTypes)
        {
            if (GetByEventType(type) == null)
                _collection.Insert(new EventConfig
                {
                    EventType = type,
                    Enabled = false,
                    Messages = new List<EventMessage> { new EventMessage { Template = GetDefaultTemplate(type), Weight = 1, Enabled = true } }
                });
        }
    }

    private static string GetDefaultTemplate(string type) => type switch
    {
        EventTypes.Follow => "%name% just followed!",
        EventTypes.Sub => "%name% just subscribed!",
        EventTypes.Resub => "%name% resubscribed for %cumulative% months!",
        EventTypes.GiftSub => "%name% gifted a sub to %recipient%!",
        EventTypes.GiftBomb => "%name% gifted %gift% subs to the community!",
        EventTypes.Cheer => "%name% cheered %bits% bits!",
        EventTypes.Raid => "%name% raided with %amount% viewers!",
        EventTypes.ChannelPoint => "%name% redeemed %title%!",
        EventTypes.HypeTrain => "Hype Train level %level%!",
        _ => "%name%"
    };
}
