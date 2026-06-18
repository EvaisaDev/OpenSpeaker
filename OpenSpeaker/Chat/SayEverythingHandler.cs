using OpenSpeaker.Data;
using OpenSpeaker.Models;
using OpenSpeaker.Queue;
using OpenSpeaker.Services;
using OpenSpeaker.Text;
using OpenSpeaker.Twitch;
using OpenSpeaker.Users;
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

    private string _lastSpeakingUser = string.Empty;
    private readonly Dictionary<string, DateTime> _lastSpoke = new();

    public SayEverythingHandler(
        SettingsRepository settingsRepo,
        UserService userService,
        PermissionChecker permissionChecker,
        MessageSanitizer sanitizer,
        ITtsQueue queue,
        VoicePool voicePool,
        ITwitchService twitch)
    {
        _settingsRepo = settingsRepo;
        _userService = userService;
        _permissionChecker = permissionChecker;
        _sanitizer = sanitizer;
        _queue = queue;
        _voicePool = voicePool;
        _twitch = twitch;
    }

    public async Task HandleAsync(string twitchId, string username, string displayName, string message, List<string> roles, bool isCommand = false, bool isHighlight = false, bool isSubscriber = false)
    {
        var settings = _settingsRepo.GetSettings();
        if (!settings.Enabled) return;

        if (twitchId == _twitch.BroadcasterId && !isCommand) return;

        if (_sanitizer.IsIgnoredPrefix(message)) return;

        var user = await _userService.GetOrCreateAsync(twitchId, username);
        if (user.IsSubscribed != isSubscriber)
            await _userService.UpdateSubscribedAsync(twitchId, isSubscriber);

        if (!_permissionChecker.CanSpeak(user, roles, settings))
        {
            if (isCommand && !string.IsNullOrWhiteSpace(settings.NotAllowedText))
                await _twitch.SendChatMessageAsync(settings.NotAllowedText);
            return;
        }

        if (settings.CooldownSeconds > 0)
        {
            var now = DateTime.UtcNow;
            if (_lastSpoke.TryGetValue(twitchId, out var last) && (now - last).TotalSeconds < settings.CooldownSeconds)
                return;
            _lastSpoke[twitchId] = now;
        }

        var sanitized = _sanitizer.Sanitize(message, true);
        if (string.IsNullOrWhiteSpace(sanitized)) return;

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
