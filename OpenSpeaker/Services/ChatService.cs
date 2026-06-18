using OpenSpeaker.Data;
using OpenSpeaker.Models;
using OpenSpeaker.Queue;
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

    public ChatService(
        ITwitchService twitch,
        Chat.BuiltInCommandHandler builtIn,
        Chat.CustomCommandHandler custom,
        Chat.SayEverythingHandler sayEverything,
        UserService userService,
        SettingsRepository settingsRepo,
        ITtsQueue queue)
    {
        _twitch = twitch;
        _builtIn = builtIn;
        _custom = custom;
        _sayEverything = sayEverything;
        _userService = userService;
        _settingsRepo = settingsRepo;
        _queue = queue;

        _twitch.ChatMessage += OnChatMessage;
        _twitch.MessageDeleted += OnMessageDeleted;
        _twitch.UserBanned += OnUserBanned;
    }

    private async void OnChatMessage(object? sender, Twitch.TwitchEventArgs.ChatMessageEventArgs e)
    {
        _ = _userService.TouchLastActiveAsync(e.UserId, e.Username);
        if (await _builtIn.HandleAsync(e.UserId, e.Username, e.Roles, e.Message)) return;
        if (await _custom.HandleAsync(e.UserId, e.Username, e.Roles, e.Message)) return;

        var settings = _settingsRepo.GetSettings();
        if (settings.Mode == TtsModes.Everything)
        {
            await _sayEverything.HandleAsync(e.UserId, e.Username, e.DisplayName, e.Message, e.Roles, isHighlight: e.IsHighlight, isSubscriber: e.IsSubscriber);
        }
        else if (settings.Mode == TtsModes.Command)
        {
            foreach (var cmd in settings.TtsCommands)
            {
                if (e.Message.StartsWith(cmd, StringComparison.OrdinalIgnoreCase))
                {
                    var text = e.Message.Substring(cmd.Length).Trim();
                    if (!string.IsNullOrEmpty(text))
                        await _sayEverything.HandleAsync(e.UserId, e.Username, e.DisplayName, text, e.Roles, isCommand: true);
                    break;
                }
            }
        }
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
