using OpenSpeaker.Models;
using OpenSpeaker.Twitch;
namespace OpenSpeaker.Events;

public class EventDispatcher
{
    private readonly ITwitchService _twitch;
    private readonly IEventProcessor _eventProcessor;
    private readonly VariableBuilder _variableBuilder;

    public EventDispatcher(ITwitchService twitch, IEventProcessor eventProcessor, VariableBuilder variableBuilder)
    {
        _twitch = twitch;
        _eventProcessor = eventProcessor;
        _variableBuilder = variableBuilder;

        _twitch.Follow += (_, e) => _ = _eventProcessor.ProcessAsync(EventTypes.Follow, _variableBuilder.FromFollow(e));
        _twitch.Sub += (_, e) => _ = _eventProcessor.ProcessAsync(EventTypes.Sub, _variableBuilder.FromSub(e));
        _twitch.Resub += (_, e) => _ = _eventProcessor.ProcessAsync(EventTypes.Resub, _variableBuilder.FromResub(e));
        _twitch.GiftSub += (_, e) => _ = _eventProcessor.ProcessAsync(EventTypes.GiftSub, _variableBuilder.FromGiftSub(e));
        _twitch.GiftBomb += (_, e) => _ = _eventProcessor.ProcessAsync(EventTypes.GiftBomb, _variableBuilder.FromGiftBomb(e));
        _twitch.Cheer += (_, e) => _ = _eventProcessor.ProcessAsync(EventTypes.Cheer, _variableBuilder.FromCheer(e));
        _twitch.Raid += (_, e) => _ = _eventProcessor.ProcessAsync(EventTypes.Raid, _variableBuilder.FromRaid(e));
        _twitch.ChannelPoint += (_, e) => _ = _eventProcessor.ProcessAsync(EventTypes.ChannelPoint, _variableBuilder.FromChannelPoint(e));
        _twitch.HypeTrain += (_, e) => _ = _eventProcessor.ProcessAsync(EventTypes.HypeTrain, _variableBuilder.FromHypeTrain(e));
        _twitch.Goal += (_, e) => _ = _eventProcessor.ProcessAsync(EventTypes.Goal, _variableBuilder.FromGoal(e));
    }
}
