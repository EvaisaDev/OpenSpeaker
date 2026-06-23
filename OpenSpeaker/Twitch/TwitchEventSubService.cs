using Microsoft.Extensions.Logging.Abstractions;
using OpenSpeaker.Data;
using OpenSpeaker.ThingsIDKWhereToPut.Logging;
using OpenSpeaker.Users;
using OpenSpeaker.Twitch.TwitchEventArgs;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
namespace OpenSpeaker.Twitch;

public class TwitchEventSubService : ITwitchService, IDisposable
{
    private readonly TwitchAuthService _auth;
    private readonly EmoteCacheService _emoteCache;
    private readonly IAppLogger? _logger;
    private readonly PermissionChecker _permissionChecker = new();
    private EventSubWebsocketClient? _wsClient;
    private TwitchAPI? _api;
    private bool _connected = false;
    private bool _hasConnectedOnce = false;

    public bool IsConnected => _connected;
    public bool IsChatConnected => _connected;
    public string BroadcasterId => _auth.GetUserId() ?? string.Empty;
    public string BroadcasterLogin => _auth.GetLogin() ?? string.Empty;

    public event EventHandler<ChatMessageEventArgs>? ChatMessage;
    public event EventHandler<FollowEventArgs>? Follow;
    public event EventHandler<SubEventArgs>? Sub;
    public event EventHandler<ResubEventArgs>? Resub;
    public event EventHandler<GiftSubEventArgs>? GiftSub;
    public event EventHandler<GiftBombEventArgs>? GiftBomb;
    public event EventHandler<CheerEventArgs>? Cheer;
    public event EventHandler<RaidEventArgs>? Raid;
    public event EventHandler<ChannelPointEventArgs>? ChannelPoint;
    public event EventHandler<HypeTrainEventArgs>? HypeTrain;
    public event EventHandler<GoalEventArgs>? Goal;
    public event EventHandler<MessageDeletedEventArgs>? MessageDeleted;
    public event EventHandler<UserBannedEventArgs>? UserBanned;

    public TwitchEventSubService(TwitchAuthService auth, EmoteCacheService emoteCache, IAppLogger? logger = null)
    {
        _auth = auth;
        _emoteCache = emoteCache;
        _logger = logger;
    }

    public async Task ConnectAsync()
    {
        _logger?.Info($"TWITCH :: ConnectAsync called. HasValidAccount={_auth.HasValidAccount()} BroadcasterId={BroadcasterId}");
        if (!_auth.HasValidAccount()) return;

        _wsClient = new EventSubWebsocketClient(NullLoggerFactory.Instance);
        _wsClient.WebsocketConnected += OnWebsocketConnected;
        _wsClient.WebsocketDisconnected += OnWebsocketDisconnected;
        _wsClient.ChannelChatMessage += OnChatMessage;
        _wsClient.ChannelFollow += OnFollow;
        _wsClient.ChannelSubscribe += OnSub;
        _wsClient.ChannelSubscriptionMessage += OnResub;
        _wsClient.ChannelSubscriptionGift += OnGiftSub;
        _wsClient.ChannelCheer += OnCheer;
        _wsClient.ChannelRaid += OnRaid;
        _wsClient.ChannelPointsCustomRewardRedemptionAdd += OnChannelPoint;
        _wsClient.ChannelHypeTrainProgressV2 += OnHypeTrain;
        _wsClient.ChannelGoalProgress += OnGoalProgress;
        _wsClient.ChannelChatMessageDelete += OnMessageDeleted;
        _wsClient.ChannelBan += OnUserBanned;

        await _wsClient.ConnectAsync();
    }

    public async Task DisconnectAsync()
    {
        _connected = false;
        _hasConnectedOnce = false;
        if (_wsClient != null)
        {
            await _wsClient.DisconnectAsync();
            _wsClient = null;
        }
    }

    public async Task SendChatMessageAsync(string message)
    {
        var accessToken = _auth.GetAccessToken();
        var clientId = _auth.GetClientId();
        var broadcasterId = BroadcasterId;
        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(broadcasterId)) return;
        try
        {
            using var http = new System.Net.Http.HttpClient();
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            http.DefaultRequestHeaders.Add("Client-Id", clientId);
            var body = System.Text.Json.JsonSerializer.Serialize(new { broadcaster_id = broadcasterId, sender_id = broadcasterId, message });
            await http.PostAsync("https://api.twitch.tv/helix/chat/messages",
                new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        }
        catch { }
    }

    private async Task OnWebsocketConnected(object? sender, WebsocketConnectedArgs e)
    {
        _connected = true;
        var broadcasterId = BroadcasterId;
        _logger?.Info($"TWITCH :: WebSocket connected. IsRequestedReconnect={e.IsRequestedReconnect} BroadcasterId={broadcasterId} SessionId={_wsClient?.SessionId}");
        if (string.IsNullOrEmpty(broadcasterId)) { _logger?.Warn("TWITCH :: BroadcasterId is empty, aborting subscription"); return; }

        if (!e.IsRequestedReconnect)
        {
            _api = new TwitchAPI();
            _api.Settings.ClientId = _auth.GetClientId() ?? string.Empty;
            _api.Settings.AccessToken = _auth.GetAccessToken() ?? string.Empty;
            _logger?.Info($"TWITCH :: Subscribing to events with ClientId={_auth.GetClientId()} TokenLength={_auth.GetAccessToken()?.Length ?? 0}");

            await SubscribeToEvents(_api, broadcasterId, _wsClient!.SessionId);

            if (!_hasConnectedOnce)
            {
                _hasConnectedOnce = true;
                await _emoteCache.RefreshAsync(broadcasterId);

                _logger?.Info("TWITCH :: Twitch Account Connected");

                var login = _auth.GetLogin() ?? broadcasterId;
                var displayName = _auth.GetDisplayName() ?? login;

                try
                {
                    var modsResponse = await _api.Helix.Moderation.GetModeratorsAsync(broadcasterId);
                    _logger?.Info($"TWITCH :: Found {modsResponse.Data.Length} moderators for the channel {login}");
                }
                catch { }

                try
                {
                    var subsResponse = await _api.Helix.Subscriptions.GetBroadcasterSubscriptionsAsync(broadcasterId);
                    _logger?.Info($"TWITCH :: {displayName} has {subsResponse.Total} subscriptions.");
                }
                catch { }

                _logger?.Info("TWITCH :: Twitch PubSub Connected");
                _logger?.Info("TWITCH :: Twitch Chat Client Connected");
            }
            else
            {
                _logger?.Info("TWITCH :: Reconnected");
            }
        }
    }

    private async Task SubscribeToEvents(TwitchAPI api, string broadcasterId, string sessionId)
    {
        var conditions = new Dictionary<string, string> { ["broadcaster_user_id"] = broadcasterId };

        await Subscribe(api, "channel.chat.message", "1", new Dictionary<string, string> { ["broadcaster_user_id"] = broadcasterId, ["user_id"] = broadcasterId }, sessionId);
        await Subscribe(api, "channel.follow", "2", new Dictionary<string, string> { ["broadcaster_user_id"] = broadcasterId, ["moderator_user_id"] = broadcasterId }, sessionId);
        await Subscribe(api, "channel.subscribe", "1", conditions, sessionId);
        await Subscribe(api, "channel.subscription.message", "1", conditions, sessionId);
        await Subscribe(api, "channel.subscription.gift", "1", conditions, sessionId);
        await Subscribe(api, "channel.cheer", "1", conditions, sessionId);
        await Subscribe(api, "channel.raid", "1", new Dictionary<string, string> { ["to_broadcaster_user_id"] = broadcasterId }, sessionId);
        await Subscribe(api, "channel.channel_points_custom_reward_redemption.add", "1", conditions, sessionId);
        await Subscribe(api, "channel.hype_train.progress", "1", conditions, sessionId);
        await Subscribe(api, "channel.goal.progress", "1", conditions, sessionId);
        await Subscribe(api, "channel.chat.message_delete", "1", new Dictionary<string, string> { ["broadcaster_user_id"] = broadcasterId, ["user_id"] = broadcasterId }, sessionId);
        await Subscribe(api, "channel.ban", "1", conditions, sessionId);
    }

    private async Task Subscribe(TwitchAPI api, string type, string version, Dictionary<string, string> conditions, string sessionId)
    {
        try
        {
            await api.Helix.EventSub.CreateEventSubSubscriptionAsync(type, version, conditions, EventSubTransportMethod.Websocket, sessionId);
            _logger?.Info($"TWITCH :: Subscribed to {type} v{version}");
        }
        catch (Exception ex)
        {
            _logger?.Warn($"TWITCH :: EventSub subscription failed for {type}: {ex.Message}");
        }
    }

    private async Task OnWebsocketDisconnected(object? sender, WebsocketDisconnectedArgs e)
    {
        _connected = false;
        _logger?.Warn("TWITCH :: WebSocket disconnected, reconnecting in 5s...");
        await Task.Delay(5000);
        if (_wsClient != null)
        {
            while (!await _wsClient.ReconnectAsync())
                await Task.Delay(1000);
        }
    }

    private async Task OnChatMessage(object? sender, ChannelChatMessageArgs e)
    {
        var msg = e.Payload.Event;
        _logger?.Info($"TWITCH :: Raw chat message from {msg.ChatterUserLogin}: {msg.Message.Text}");
        var roles = _permissionChecker.DetermineRoles(msg.IsBroadcaster, msg.IsModerator, msg.IsSubscriber, msg.IsVip);

        var messageEmotes = msg.Message.Fragments?
            .Where(f => f.Type == "emote")
            .Select(f => f.Text)
            .ToList() ?? [];
        var messageCheermotes = msg.Message.Fragments?
            .Where(f => f.Type == "cheermote")
            .Select(f => f.Text)
            .ToList() ?? [];

        ChatMessage?.Invoke(this, new ChatMessageEventArgs
        {
            UserId = msg.ChatterUserId,
            Username = msg.ChatterUserLogin,
            DisplayName = msg.ChatterUserName,
            Message = msg.Message.Text,
            IsCheer = msg.Cheer != null,
            Bits = msg.Cheer?.Bits ?? 0,
            Roles = roles,
            IsSubscriber = msg.IsSubscriber,
            IsHighlight = msg.MessageType == "channel_points_highlighted",
            MessageEmotes = messageEmotes,
            MessageCheermotes = messageCheermotes,
        });
        await Task.CompletedTask;
    }

    private async Task OnFollow(object? sender, ChannelFollowArgs e)
    {
        var ev = e.Payload.Event;
        Follow?.Invoke(this, new FollowEventArgs { UserId = ev.UserId, Username = ev.UserLogin });
        await Task.CompletedTask;
    }

    private async Task OnSub(object? sender, ChannelSubscribeArgs e)
    {
        var ev = e.Payload.Event;
        Sub?.Invoke(this, new SubEventArgs { UserId = ev.UserId, Username = ev.UserLogin, Tier = ev.Tier, IsGift = ev.IsGift });
        await Task.CompletedTask;
    }

    private async Task OnResub(object? sender, ChannelSubscriptionMessageArgs e)
    {
        var ev = e.Payload.Event;
        Resub?.Invoke(this, new ResubEventArgs
        {
            UserId = ev.UserId,
            Username = ev.UserLogin,
            Tier = ev.Tier,
            CumulativeMonths = ev.CumulativeMonths,
            Message = ev.Message?.Text ?? string.Empty
        });
        await Task.CompletedTask;
    }

    private async Task OnGiftSub(object? sender, ChannelSubscriptionGiftArgs e)
    {
        var ev = e.Payload.Event;
        if (ev.IsAnonymous || ev.Total > 1)
        {
            GiftBomb?.Invoke(this, new GiftBombEventArgs
            {
                GiverId = ev.UserId ?? "anonymous",
                GiverUsername = ev.UserLogin ?? "anonymous",
                GiftCount = ev.Total,
                Tier = ev.Tier
            });
        }
        else
        {
            GiftSub?.Invoke(this, new GiftSubEventArgs
            {
                GiverId = ev.UserId ?? "anonymous",
                GiverUsername = ev.UserLogin ?? "anonymous",
                Tier = ev.Tier
            });
        }
        await Task.CompletedTask;
    }

    private async Task OnCheer(object? sender, ChannelCheerArgs e)
    {
        var ev = e.Payload.Event;
        Cheer?.Invoke(this, new CheerEventArgs
        {
            UserId = ev.UserId ?? string.Empty,
            Username = ev.UserLogin ?? "anonymous",
            Bits = ev.Bits,
            Message = ev.Message
        });
        await Task.CompletedTask;
    }

    private async Task OnRaid(object? sender, ChannelRaidArgs e)
    {
        var ev = e.Payload.Event;
        Raid?.Invoke(this, new RaidEventArgs
        {
            FromUserId = ev.FromBroadcasterUserId,
            FromUsername = ev.FromBroadcasterUserLogin,
            ViewerCount = ev.Viewers
        });
        await Task.CompletedTask;
    }

    private async Task OnChannelPoint(object? sender, ChannelPointsCustomRewardRedemptionArgs e)
    {
        var ev = e.Payload.Event;
        ChannelPoint?.Invoke(this, new ChannelPointEventArgs
        {
            UserId = ev.UserId,
            Username = ev.UserLogin,
            RewardId = ev.Reward.Id,
            RewardTitle = ev.Reward.Title,
            RewardCost = ev.Reward.Cost,
            Input = ev.UserInput
        });
        await Task.CompletedTask;
    }

    private async Task OnHypeTrain(object? sender, ChannelHypeTrainProgressV2Args e)
    {
        var ev = e.Payload.Event;
        HypeTrain?.Invoke(this, new HypeTrainEventArgs
        {
            Level = ev.Level,
            Progress = ev.Progress,
            Total = ev.Goal
        });
        await Task.CompletedTask;
    }

    private async Task OnGoalProgress(object? sender, ChannelGoalProgressArgs e)
    {
        var ev = e.Payload.Event;
        Goal?.Invoke(this, new GoalEventArgs
        {
            CurrentAmount = ev.CurrentAmount,
            TargetAmount = ev.TargetAmount,
            Type = ev.Type
        });
        await Task.CompletedTask;
    }

    private async Task OnMessageDeleted(object? sender, ChannelChatMessageDeleteArgs e)
    {
        var ev = e.Payload.Event;
        MessageDeleted?.Invoke(this, new MessageDeletedEventArgs
        {
            UserId = ev.TargetUserId,
            Username = ev.TargetUserLogin,
            MessageId = ev.MessageId
        });
        await Task.CompletedTask;
    }

    private async Task OnUserBanned(object? sender, ChannelBanArgs e)
    {
        var ev = e.Payload.Event;
        UserBanned?.Invoke(this, new UserBannedEventArgs
        {
            UserId = ev.UserId,
            Username = ev.UserLogin,
            IsPermanent = ev.IsPermanent
        });
        await Task.CompletedTask;
    }

    public void Dispose() => DisconnectAsync().GetAwaiter().GetResult();
}
