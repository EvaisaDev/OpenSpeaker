using OpenSpeaker.Twitch.TwitchEventArgs;
namespace OpenSpeaker.Twitch;
public interface ITwitchService
{
    bool IsConnected { get; }
    bool IsChatConnected { get; }
    string BroadcasterId { get; }
    string BroadcasterLogin { get; }
    Task ConnectAsync();
    Task DisconnectAsync();
    Task SendChatMessageAsync(string message);

    event EventHandler<ChatMessageEventArgs> ChatMessage;
    event EventHandler<FollowEventArgs> Follow;
    event EventHandler<SubEventArgs> Sub;
    event EventHandler<ResubEventArgs> Resub;
    event EventHandler<GiftSubEventArgs> GiftSub;
    event EventHandler<GiftBombEventArgs> GiftBomb;
    event EventHandler<CheerEventArgs> Cheer;
    event EventHandler<RaidEventArgs> Raid;
    event EventHandler<ChannelPointEventArgs> ChannelPoint;
    event EventHandler<HypeTrainEventArgs> HypeTrain;
    event EventHandler<GoalEventArgs> Goal;
    event EventHandler<MessageDeletedEventArgs> MessageDeleted;
    event EventHandler<UserBannedEventArgs> UserBanned;
}
