using OpenSpeaker.Data;
using OpenSpeaker.Models;
using OpenSpeaker.Queue;
using OpenSpeaker.Services;
using OpenSpeaker.TTS;
using OpenSpeaker.Twitch;
using OpenSpeaker.Users;
namespace OpenSpeaker.Chat;

public class BuiltInCommandHandler : IChatCommandHandler
{
    private readonly SettingsRepository _settingsRepo;
    private readonly ITtsQueue _queue;
    private readonly UserService _userService;
    private readonly UserRepository _userRepo;
    private readonly TtsEngineRegistry _engineRegistry;
    private readonly EventConfigRepository _eventConfigRepo;
    private readonly CustomCommandRepository _customCommandRepo;
    private readonly ITwitchService _twitch;
    private readonly VoicePool _voicePool;

    public BuiltInCommandHandler(
        SettingsRepository settingsRepo,
        ITtsQueue queue,
        UserService userService,
        UserRepository userRepo,
        TtsEngineRegistry engineRegistry,
        EventConfigRepository eventConfigRepo,
        CustomCommandRepository customCommandRepo,
        ITwitchService twitch,
        VoicePool voicePool)
    {
        _settingsRepo = settingsRepo;
        _queue = queue;
        _userService = userService;
        _userRepo = userRepo;
        _engineRegistry = engineRegistry;
        _eventConfigRepo = eventConfigRepo;
        _customCommandRepo = customCommandRepo;
        _twitch = twitch;
        _voicePool = voicePool;
    }

    public async Task<bool> HandleAsync(string twitchId, string username, List<string> roles, string rawMessage)
    {
        var settings = _settingsRepo.GetSettings();
        var cmdName = settings.BuiltInCommandName;

        if (!rawMessage.StartsWith(cmdName, StringComparison.OrdinalIgnoreCase))
            return false;

        var isMod = roles.Contains(UserRoles.Moderator) || roles.Contains(UserRoles.Broadcaster);
        var rest = rawMessage.Substring(cmdName.Length).Trim();
        var parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
            return true;

        var sub = parts[0].ToLower();

        switch (sub)
        {
            case "pause" when isMod:
                _queue.Pause();
                if (!settings.SilenceCommandOutput)
                    await _twitch.SendChatMessageAsync("TTS paused.");
                return true;

            case "resume" when isMod:
                _queue.Resume();
                if (!settings.SilenceCommandOutput)
                    await _twitch.SendChatMessageAsync("TTS resumed.");
                return true;

            case "clear" when isMod:
                _queue.Clear();
                if (!settings.SilenceCommandOutput)
                    await _twitch.SendChatMessageAsync("TTS queue cleared.");
                return true;

            case "stop" when isMod:
                _queue.Stop();
                if (!settings.SilenceCommandOutput)
                    await _twitch.SendChatMessageAsync("TTS stopped.");
                return true;

            case "mode" when isMod && parts.Length > 1:
                var mode = parts[1].ToLower() == "all" ? TtsModes.Everything : TtsModes.Command;
                settings.Mode = mode;
                _settingsRepo.SaveSettings(settings);
                if (!settings.SilenceCommandOutput)
                    await _twitch.SendChatMessageAsync($"TTS mode set to {(mode == TtsModes.Everything ? "all" : "command")}.");
                return true;

            case "on" when isMod:
            case "enable" when isMod:
                settings.Enabled = true;
                _settingsRepo.SaveSettings(settings);
                if (!settings.SilenceCommandOutput)
                    await _twitch.SendChatMessageAsync("TTS enabled.");
                return true;

            case "off" when isMod:
            case "disable" when isMod:
                settings.Enabled = false;
                _settingsRepo.SaveSettings(settings);
                if (!settings.SilenceCommandOutput)
                    await _twitch.SendChatMessageAsync("TTS disabled.");
                return true;

            case "events" when isMod && parts.Length > 1:
                settings.EventsEnabled = parts[1].ToLower() == "on";
                _settingsRepo.SaveSettings(settings);
                if (!settings.SilenceCommandOutput)
                    await _twitch.SendChatMessageAsync($"TTS events {(settings.EventsEnabled ? "enabled" : "disabled")}.");
                return true;

            case "ignore" when isMod && parts.Length > 2:
                var ignoreMode = parts[1].ToLower();
                var ignoreUser = parts[2];
                await _userService.SetIgnoredAsync(ignoreUser, ignoreMode == "add");
                if (!settings.SilenceCommandOutput)
                    await _twitch.SendChatMessageAsync($"{ignoreUser} {(ignoreMode == "add" ? "added to" : "removed from")} ignore list.");
                return true;

            case "ignored":
                var ignoredUsers = _userRepo.GetIgnored();
                if (ignoredUsers.Count == 0)
                    await _twitch.SendChatMessageAsync("No ignored users.");
                else
                    await _twitch.SendChatMessageAsync($"Ignored: {string.Join(", ", ignoredUsers.Select(u => u.Username))}");
                return true;

            case "reg" when isMod && parts.Length > 2:
                var regMode = parts[1].ToLower();
                var regUser = parts[2];
                await _userService.SetRegularAsync(regUser, regMode == "add");
                if (!settings.SilenceCommandOutput)
                    await _twitch.SendChatMessageAsync($"{regUser} {(regMode == "add" ? "added as" : "removed as")} regular.");
                return true;

            case "random" when parts.Length > 1 && parts[1].ToLower() == "reset":
                var resetUser = parts.Length > 2 ? parts[2] : username;
                if (isMod || resetUser.Equals(username, StringComparison.OrdinalIgnoreCase))
                {
                    await _userService.ResetRandomVoiceAsync(resetUser);
                    if (!settings.SilenceCommandOutput)
                        await _twitch.SendChatMessageAsync($"{resetUser}'s random voice has been reset.");
                }
                return true;

            case "set" when isMod && parts.Length > 2:
                var setMethod = parts[1].ToLower();
                if (setMethod == "nickname" && parts.Length > 3)
                {
                    await _userService.SetNicknameAsync(parts[2], parts[3]);
                    if (!settings.SilenceCommandOutput)
                        await _twitch.SendChatMessageAsync($"{parts[2]}'s nickname set to {parts[3]}.");
                    return true;
                }
                if (setMethod == "sticky" && parts.Length > 2)
                {
                    settings.StickyRandomVoice = parts[2].ToLower() == "on";
                    _settingsRepo.SaveSettings(settings);
                    if (!settings.SilenceCommandOutput)
                        await _twitch.SendChatMessageAsync($"Sticky random voice {(settings.StickyRandomVoice ? "enabled" : "disabled")}.");
                    return true;
                }
                return true;

            case "assign" when isMod && parts.Length > 2 && parts[1].ToLower() == "last":
                var (lastVoiceId, lastEngineId) = _queue.LastUsedVoice;
                if (!string.IsNullOrEmpty(lastVoiceId))
                {
                    await _userService.AssignLastVoiceAsync(parts[2], lastVoiceId, lastEngineId);
                    if (!settings.SilenceCommandOutput)
                        await _twitch.SendChatMessageAsync($"Assigned last used voice to {parts[2]}.");
                }
                return true;

            case "status":
                await _twitch.SendChatMessageAsync(
                    $"OpenSpeaker: {(settings.Enabled ? "enabled" : "disabled")}, mode: {settings.Mode}, queue: {_queue.Count} item(s).");
                return true;

            case "voices":
                var cachedVoices = await _voicePool.GetAllAsync();
                if (cachedVoices.Count == 0)
                    await _twitch.SendChatMessageAsync("No voices loaded yet.");
                else
                {
                    var summary = cachedVoices
                        .GroupBy(v => v.EngineId)
                        .Select(g => $"{_engineRegistry.GetDisplayName(g.Key)}: {g.Count()}")
                        .ToList();
                    await _twitch.SendChatMessageAsync($"Voices ({cachedVoices.Count} total) — {string.Join(", ", summary)}");
                }
                return true;

            case "commands":
                var cmds = _customCommandRepo.GetAll().Where(c => c.Enabled).ToList();
                if (cmds.Count == 0)
                    await _twitch.SendChatMessageAsync("No custom commands configured.");
                else
                    await _twitch.SendChatMessageAsync($"Commands: {string.Join(", ", cmds.Select(c => c.Trigger))}");
                return true;

            case "about":
            case "aboot":
                await _twitch.SendChatMessageAsync($"OpenSpeaker — open source TTS bot. Instance: {settings.InstanceName}");
                return true;

            default:
                return false;
        }
    }
}
