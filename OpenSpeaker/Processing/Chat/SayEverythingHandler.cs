using OpenSpeaker.Data;
using OpenSpeaker.Extensions;
using OpenSpeaker.Models;
using OpenSpeaker.Queue;
using OpenSpeaker.Services;
using OpenSpeaker.Text;
using OpenSpeaker.Infrastructure.Logging;
using OpenSpeaker.Twitch;
using OpenSpeaker.Users;
using System.Collections.Concurrent;
namespace OpenSpeaker.Chat;

public class SayEverythingHandler
{
    private readonly SettingsRepository _settingsRepo;
    private readonly UserService _userService;
    private readonly PermissionChecker _permissionChecker;
    private readonly MessageSanitizer _sanitizer;
    private readonly ITtsQueue _queue;
    private readonly VoicePool _voicePool;
    private readonly ITwitchService _twitch;
    private readonly ExtensionManager? _extensions;
    private readonly IAppLogger? _logger;

    private string _lastSpeakingUser = string.Empty;
    private readonly ConcurrentDictionary<string, DateTime> _lastSpoke = new();
    private DateTime _lastSpokePrune = DateTime.UtcNow;

    public SayEverythingHandler(
        SettingsRepository settingsRepo,
        UserService userService,
        PermissionChecker permissionChecker,
        MessageSanitizer sanitizer,
        ITtsQueue queue,
        VoicePool voicePool,
        ITwitchService twitch,
        ExtensionManager? extensions = null,
        IAppLogger? logger = null)
    {
        _settingsRepo = settingsRepo;
        _userService = userService;
        _permissionChecker = permissionChecker;
        _sanitizer = sanitizer;
        _queue = queue;
        _voicePool = voicePool;
        _twitch = twitch;
        _extensions = extensions;
        _logger = logger;
    }

    public async Task HandleAsync(string twitchId, string username, string displayName, string message, List<string> roles, bool isCommand = false, bool isReply = false, bool isHighlight = false, bool isSubscriber = false, IReadOnlyList<string>? messageEmotes = null, IReadOnlyList<string>? messageCheermotes = null)
    {
        _logger?.Info($"SAY :: HandleAsync {username}: {message} [isCommand={isCommand}]");
        var settings = _settingsRepo.GetSettings();
        if (!settings.Enabled) { _logger?.Info("SAY :: Dropped - bot disabled"); return; }

        var prefixCheckMessage = isReply ? MentionStripper.StripLeadingMention(message) : message;
        if (_sanitizer.IsIgnoredPrefix(prefixCheckMessage)) { _logger?.Info($"SAY :: Dropped - ignored prefix"); return; }

        var user = await _userService.GetOrCreateAsync(twitchId, username);
        _logger?.Info($"SAY :: User lookup: TwitchId={twitchId} Username={user.Username} IsIgnored={user.IsIgnored} IsForced={user.IsForced} IsRegular={user.IsRegular} IsSubscribed={user.IsSubscribed} Role={user.Role}");
        _logger?.Info($"SAY :: Roles from Twitch: [{string.Join(", ", roles)}]");
        _logger?.Info($"SAY :: Settings: AllowEveryone={settings.AllowEveryone} AllowSubs={settings.AllowSubscribers} AllowMods={settings.AllowModerators} AllowVIPs={settings.AllowVIPs} AllowRegulars={settings.AllowRegulars}");
        if (user.IsSubscribed != isSubscriber)
            await _userService.UpdateSubscribedAsync(twitchId, isSubscriber);

        if (!_permissionChecker.CanSpeak(user, roles, settings))
        {
            _logger?.Info($"SAY :: Dropped - no permission (IsIgnored={user.IsIgnored} IsForced={user.IsForced})");
            if (isCommand && !string.IsNullOrWhiteSpace(settings.NotAllowedText))
                await _twitch.SendChatMessageAsync(settings.NotAllowedText);
            return;
        }

        if (settings.CooldownSeconds > 0)
        {
            var now = DateTime.UtcNow;
            if (_lastSpoke.TryGetValue(twitchId, out var last) && (now - last).TotalSeconds < settings.CooldownSeconds)
            { _logger?.Info($"SAY :: Dropped - cooldown ({settings.CooldownSeconds}s)"); return; }
            _lastSpoke[twitchId] = now;

            if ((now - _lastSpokePrune).TotalSeconds > settings.CooldownSeconds)
            {
                _lastSpokePrune = now;
                foreach (var entry in _lastSpoke)
                    if ((now - entry.Value).TotalSeconds >= settings.CooldownSeconds)
                        _lastSpoke.TryRemove(entry.Key, out _);
            }
        }

        var sanitized = _sanitizer.Sanitize(message, true, messageEmotes, messageCheermotes);
        _logger?.Info($"SAY :: Sanitized='{sanitized}'");
        if (string.IsNullOrWhiteSpace(sanitized)) { _logger?.Info("SAY :: Dropped - sanitized to empty"); return; }

        if (_extensions is { HasMessageFilters: true })
        {
            var ctx = new MessageFilterContext(
                twitchId, username, displayName, user.Nickname,
                isSubscriber,
                roles.Contains(UserRoles.Moderator, StringComparer.OrdinalIgnoreCase),
                roles.Contains(UserRoles.VIP, StringComparer.OrdinalIgnoreCase),
                roles.Contains(UserRoles.Broadcaster, StringComparer.OrdinalIgnoreCase),
                user.IsRegular, user.IsIgnored, user.IsForced
            );
            sanitized = await _extensions.ProcessMessageAsync(ctx, sanitized);
            if (string.IsNullOrWhiteSpace(sanitized)) { _logger?.Info("SAY :: Dropped - extension filtered"); return; }
        }

        string finalText;
        if (settings.SayUsername)
        {
            var displayNameForSpeech = settings.ReplaceNameWithNickname && !string.IsNullOrEmpty(user.Nickname)
                ? user.Nickname : displayName;
            bool skipUsername = settings.OnlySayUsernameIfDifferent && _lastSpeakingUser.Equals(username, StringComparison.OrdinalIgnoreCase);
            finalText = skipUsername ? sanitized : $"{displayNameForSpeech} {settings.SayUsernamePrefix} {sanitized}";
        }
        else
        {
            finalText = sanitized;
        }

        _lastSpeakingUser = username;

        var item = new TtsQueueItem { Text = finalText, UserId = twitchId, Username = username };
        _logger?.Info($"SAY :: Enqueueing '{finalText}' AliasName='{user.AliasName}' DefaultAlias='{settings.DefaultVoiceAlias}'");

        if (isHighlight && settings.UseHighlightVoice && !string.IsNullOrEmpty(settings.HighlightVoiceAlias))
        {
            item.VoiceAliasName = settings.HighlightVoiceAlias;
            _queue.Enqueue(item);
            return;
        }

        if (!string.IsNullOrEmpty(user.AliasName))
        {
            item.VoiceAliasName = user.AliasName;
        }
        else if (settings.StickyRandomVoice)
        {
            if (!string.IsNullOrEmpty(user.StickyVoiceId))
            {
                item.StickyVoiceId = user.StickyVoiceId;
                item.StickyVoiceEngineId = user.StickyVoiceEngineId;
            }
            else
            {
                var pick = await _voicePool.GetRandomAsync();
                if (pick.HasValue)
                {
                    await _userService.SetStickyRandomVoiceAsync(twitchId, pick.Value.Voice.Id, pick.Value.EngineId);
                    item.StickyVoiceId = pick.Value.Voice.Id;
                    item.StickyVoiceEngineId = pick.Value.EngineId;
                }
                else
                {
                    item.VoiceAliasName = settings.DefaultVoiceAlias;
                }
            }
        }
        else
        {
            item.VoiceAliasName = settings.DefaultVoiceAlias;
        }

        _queue.Enqueue(item);
    }
}
