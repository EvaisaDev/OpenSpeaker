using OpenSpeaker.Data;
using OpenSpeaker.Extensions;
using OpenSpeaker.Models;
using OpenSpeaker.Queue;
using OpenSpeaker.Infrastructure.Logging;
using OpenSpeaker.Twitch;
using OpenSpeaker.Users;
namespace OpenSpeaker.Services;

public class ChatService
{
    private readonly ITwitchService _twitch;
    private readonly Chat.BuiltInCommandHandler _builtIn;
    private readonly Chat.CustomCommandHandler _custom;
    private readonly Chat.SayEverythingHandler _sayEverything;
    private readonly UserService _userService;
    private readonly SettingsRepository _settingsRepo;
    private readonly ITtsQueue _queue;
    private readonly ExtensionManager? _extensions;
    private readonly IAppLogger? _logger;

    public ChatService(
        ITwitchService twitch,
        Chat.BuiltInCommandHandler builtIn,
        Chat.CustomCommandHandler custom,
        Chat.SayEverythingHandler sayEverything,
        UserService userService,
        SettingsRepository settingsRepo,
        ITtsQueue queue,
        ExtensionManager? extensions = null,
        IAppLogger? logger = null)
    {
        _twitch = twitch;
        _builtIn = builtIn;
        _custom = custom;
        _sayEverything = sayEverything;
        _userService = userService;
        _settingsRepo = settingsRepo;
        _queue = queue;
        _extensions = extensions;
        _logger = logger;

        _twitch.ChatMessage += OnChatMessage;
        _twitch.MessageDeleted += OnMessageDeleted;
        _twitch.UserBanned += OnUserBanned;
    }

    private async void OnChatMessage(object? sender, Twitch.TwitchEventArgs.ChatMessageEventArgs e)
    {
        _logger?.Info($"CHAT :: Message from {e.Username}: {e.Message}");
        _userService.TouchLastActiveAsync(e.UserId, e.Username).Forget(_logger, "TouchLastActive");

        await NotifyChatObserversAsync(e);

        var matchMessage = e.IsReply ? Text.MentionStripper.StripLeadingMention(e.Message) : e.Message;
        if (matchMessage != e.Message) _logger?.Info($"CHAT :: Reply mention stripped for matching → '{matchMessage}'");

        if (await _builtIn.HandleAsync(e.UserId, e.Username, e.Roles, matchMessage)) { _logger?.Info("CHAT :: Handled as built-in command"); return; }
        if (await _custom.HandleAsync(e.UserId, e.Username, e.Roles, matchMessage)) { _logger?.Info("CHAT :: Handled as custom command"); return; }

        var settings = _settingsRepo.GetSettings();
        _logger?.Info($"CHAT :: Mode={settings.Mode} Enabled={settings.Enabled}");
        if (settings.Mode == TtsModes.Everything)
        {
            await _sayEverything.HandleAsync(e.UserId, e.Username, e.DisplayName, e.Message, e.Roles, isReply: e.IsReply, isHighlight: e.IsHighlight, isSubscriber: e.IsSubscriber, messageEmotes: e.MessageEmotes, messageCheermotes: e.MessageCheermotes);
        }
        else if (settings.Mode == TtsModes.Command)
        {
            foreach (var cmd in settings.TtsCommands)
            {
                if (matchMessage.StartsWith(cmd, StringComparison.OrdinalIgnoreCase))
                {
                    var text = matchMessage.Substring(cmd.Length).Trim();
                    if (!string.IsNullOrEmpty(text))
                        await _sayEverything.HandleAsync(e.UserId, e.Username, e.DisplayName, text, e.Roles, isCommand: true, messageEmotes: e.MessageEmotes, messageCheermotes: e.MessageCheermotes);
                    break;
                }
            }
        }
    }

    private async Task NotifyChatObserversAsync(Twitch.TwitchEventArgs.ChatMessageEventArgs e)
    {
        if (_extensions is not { HasChatObservers: true }) return;
        try
        {
            var user = await _userService.GetOrCreateAsync(e.UserId, e.Username);
            var ctx = new MessageFilterContext(
                e.UserId, e.Username, e.DisplayName, user.Nickname,
                e.IsSubscriber,
                e.Roles.Contains(UserRoles.Moderator, StringComparer.OrdinalIgnoreCase),
                e.Roles.Contains(UserRoles.VIP, StringComparer.OrdinalIgnoreCase),
                e.Roles.Contains(UserRoles.Broadcaster, StringComparer.OrdinalIgnoreCase),
                user.IsRegular, user.IsIgnored, user.IsForced
            );
            await _extensions.ObserveMessageAsync(ctx, e.Message);
        }
        catch (Exception ex) { _logger?.Error($"CHAT :: OnChat observer error: {ex.Message}"); }
    }

    private void OnMessageDeleted(object? sender, Twitch.TwitchEventArgs.MessageDeletedEventArgs e)
    {
        var settings = _settingsRepo.GetSettings();
        if (settings.StopOnMessageDeleted) _queue.StopUser(e.UserId);
        if (settings.SkipOnMessageDeleted) _queue.SkipUser(e.UserId);
    }

    private void OnUserBanned(object? sender, Twitch.TwitchEventArgs.UserBannedEventArgs e)
    {
        var settings = _settingsRepo.GetSettings();
        if (e.IsPermanent)
        {
            if (settings.StopOnUserBanned) _queue.StopUser(e.UserId);
            if (settings.SkipOnUserBanned) _queue.SkipUser(e.UserId);
        }
        else
        {
            if (settings.StopOnUserTimedOut) _queue.StopUser(e.UserId);
            if (settings.SkipOnUserTimedOut) _queue.SkipUser(e.UserId);
        }
    }
}
