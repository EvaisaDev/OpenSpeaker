using OpenSpeaker.Infrastructure.Logging;
using OpenSpeaker.Models;
using OpenSpeaker.Twitch;
namespace OpenSpeaker.Events;

public class EventDispatcher
{
    private readonly ITwitchService _twitch;
    private readonly IEventProcessor _eventProcessor;
    private readonly VariableBuilder _variableBuilder;
    private readonly IAppLogger? _logger;

    public EventDispatcher(ITwitchService twitch, IEventProcessor eventProcessor, VariableBuilder variableBuilder, IAppLogger? logger = null)
    {
        _twitch = twitch;
        _eventProcessor = eventProcessor;
        _variableBuilder = variableBuilder;
        _logger = logger;

        _twitch.Follow += (_, e) => _eventProcessor.ProcessAsync(EventTypes.Follow, _variableBuilder.FromFollow(e)).Forget(_logger, $"event:{EventTypes.Follow}");
        _twitch.Sub += (_, e) => _eventProcessor.ProcessAsync(EventTypes.Sub, _variableBuilder.FromSub(e)).Forget(_logger, $"event:{EventTypes.Sub}");
        _twitch.Resub += (_, e) => _eventProcessor.ProcessAsync(EventTypes.Resub, _variableBuilder.FromResub(e)).Forget(_logger, $"event:{EventTypes.Resub}");
        _twitch.GiftSub += (_, e) => _eventProcessor.ProcessAsync(EventTypes.GiftSub, _variableBuilder.FromGiftSub(e)).Forget(_logger, $"event:{EventTypes.GiftSub}");
        _twitch.GiftBomb += (_, e) => _eventProcessor.ProcessAsync(EventTypes.GiftBomb, _variableBuilder.FromGiftBomb(e)).Forget(_logger, $"event:{EventTypes.GiftBomb}");
        _twitch.Cheer += (_, e) => _eventProcessor.ProcessAsync(EventTypes.Cheer, _variableBuilder.FromCheer(e)).Forget(_logger, $"event:{EventTypes.Cheer}");
        _twitch.Raid += (_, e) => _eventProcessor.ProcessAsync(EventTypes.Raid, _variableBuilder.FromRaid(e)).Forget(_logger, $"event:{EventTypes.Raid}");
        _twitch.ChannelPoint += (_, e) => _eventProcessor.ProcessAsync(EventTypes.ChannelPoint, _variableBuilder.FromChannelPoint(e)).Forget(_logger, $"event:{EventTypes.ChannelPoint}");
        _twitch.HypeTrain += (_, e) => _eventProcessor.ProcessAsync(EventTypes.HypeTrain, _variableBuilder.FromHypeTrain(e)).Forget(_logger, $"event:{EventTypes.HypeTrain}");
        _twitch.Goal += (_, e) => _eventProcessor.ProcessAsync(EventTypes.Goal, _variableBuilder.FromGoal(e)).Forget(_logger, $"event:{EventTypes.Goal}");
    }
}
