using OpenSpeaker.Models;
namespace OpenSpeaker.Events;

public class WeightedMessagePicker
{
    private readonly Random _rng = new();

    public EventMessage? Pick(IEnumerable<EventMessage> messages)
    {
        var enabled = messages.Where(m => m.Enabled && m.Weight > 0).ToList();
        if (enabled.Count == 0) return null;

        var totalWeight = enabled.Sum(m => m.Weight);
        var roll = _rng.Next(0, totalWeight);

        int cumulative = 0;
        foreach (var msg in enabled)
        {
            cumulative += msg.Weight;
            if (roll < cumulative) return msg;
        }

        return enabled.Last();
    }
}
