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
    private readonly CustomCommandRepository _customCommandRepo;
    private readonly ITwitchService _twitch;
    private readonly VoicePool _voicePool;

    public BuiltInCommandHandler(
        SettingsRepository settingsRepo,
        ITtsQueue queue,
        UserService userService,
        UserRepository userRepo,
        TtsEngineRegistry engineRegistry,
        CustomCommandRepository customCommandRepo,
        ITwitchService twitch,
        VoicePool voicePool)
    {
        _settingsRepo = settingsRepo;
        _queue = queue;
        _userService = userService;
        _userRepo = userRepo;
        _engineRegistry = engineRegistry;
        _customCommandRepo = customCommandRepo;
        _twitch = twitch;
        _voicePool = voicePool;
    }

    private async Task Reply(AppSettings settings, BuiltInCommandConfig cfg, Dictionary<string, string>? values = null)
    {
        if (settings.SilenceCommandOutput) return;
        var text = RenderReply(cfg.Reply, values);
        if (!string.IsNullOrWhiteSpace(text))
            await _twitch.SendChatMessageAsync(text);
    }

    private static string RenderReply(string template, Dictionary<string, string>? values)
    {
        if (string.IsNullOrEmpty(template) || values == null) return template ?? string.Empty;
        var text = template;
        foreach (var (key, value) in values)
            text = text.Replace("{" + key + "}", value, StringComparison.OrdinalIgnoreCase);
        return text;
    }

    private static IEnumerable<string> SplitKeywords(string keyword) =>
        keyword.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
               .Where(k => k.Length > 0);

    private static bool IsAllowed(BuiltInCommandConfig cfg, List<string> roles) =>
        roles.Contains(UserRoles.Broadcaster) || cfg.AllowedRoles.Any(roles.Contains);

    public async Task<bool> HandleAsync(string twitchId, string username, List<string> roles, string rawMessage)
    {
        var settings = _settingsRepo.GetSettings();

        if (!rawMessage.StartsWith(settings.BuiltInCommandName, StringComparison.OrdinalIgnoreCase))
            return false;

        var rest = rawMessage.Substring(settings.BuiltInCommandName.Length).Trim();
        if (rest.Length == 0)
            return true;

        var candidates = BuiltInCommandCatalog.Resolve(settings)
            .Where(c => c.Enabled)
            .SelectMany(c => SplitKeywords(c.Keyword).Select(kw => (cfg: c, kw)))
            .OrderByDescending(x => x.kw.Length);

        BuiltInCommandConfig? match = null;
        string[] args = Array.Empty<string>();
        foreach (var (cfg, kw) in candidates)
        {
            if (rest.Equals(kw, StringComparison.OrdinalIgnoreCase))
            {
                match = cfg;
                args = Array.Empty<string>();
                break;
            }
            if (rest.StartsWith(kw + " ", StringComparison.OrdinalIgnoreCase))
            {
                match = cfg;
                args = rest.Substring(kw.Length).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                break;
            }
        }

        if (match == null)
            return false;

        var selfReset = match.Id == "random_reset" && (args.Length == 0 || args[0].Equals(username, StringComparison.OrdinalIgnoreCase));
        if (!IsAllowed(match, roles) && !selfReset)
            return true;

        await Execute(match.Id, match, settings, twitchId, username, args);
        return true;
    }

    private async Task Execute(string id, BuiltInCommandConfig cfg, AppSettings settings, string twitchId, string username, string[] args)
    {
        switch (id)
        {
            case "pause":
                _queue.Pause();
                await Reply(settings, cfg);
                break;

            case "resume":
                _queue.Resume();
                await Reply(settings, cfg);
                break;

            case "clear":
                _queue.Clear();
                await Reply(settings, cfg);
                break;

            case "stop":
                _queue.Stop();
                await Reply(settings, cfg);
                break;

            case "mode":
                if (args.Length == 0) break;
                var modeAll = args[0].Equals("all", StringComparison.OrdinalIgnoreCase);
                _settingsRepo.Update(s => s.Mode = modeAll ? TtsModes.Everything : TtsModes.Command);
                await Reply(settings, cfg, new() { ["mode"] = modeAll ? "all" : "command" });
                break;

            case "enable":
                _settingsRepo.Update(s => s.Enabled = true);
                await Reply(settings, cfg);
                break;

            case "disable":
                _settingsRepo.Update(s => s.Enabled = false);
                await Reply(settings, cfg);
                break;

            case "events":
                if (args.Length == 0) break;
                var eventsOn = args[0].Equals("on", StringComparison.OrdinalIgnoreCase);
                _settingsRepo.Update(s => s.EventsEnabled = eventsOn);
                await Reply(settings, cfg, new() { ["state"] = eventsOn ? "enabled" : "disabled" });
                break;

            case "ignore":
                if (args.Length < 2) break;
                var ignoreAdd = args[0].Equals("add", StringComparison.OrdinalIgnoreCase);
                await _userService.SetIgnoredAsync(args[1], ignoreAdd);
                await Reply(settings, cfg, new() { ["user"] = args[1], ["action"] = ignoreAdd ? "added to" : "removed from" });
                break;

            case "ignored":
                var ignored = _userRepo.GetIgnored();
                await Reply(settings, cfg, new()
                {
                    ["users"] = ignored.Count == 0 ? "none" : string.Join(", ", ignored.Select(u => u.Username)),
                    ["count"] = ignored.Count.ToString(),
                });
                break;

            case "reg":
                if (args.Length < 2) break;
                var regAdd = args[0].Equals("add", StringComparison.OrdinalIgnoreCase);
                await _userService.SetRegularAsync(args[1], regAdd);
                await Reply(settings, cfg, new() { ["user"] = args[1], ["action"] = regAdd ? "added as" : "removed as" });
                break;

            case "random_reset":
                var resetUser = args.Length > 0 ? args[0] : username;
                await _userService.ResetRandomVoiceAsync(resetUser);
                await Reply(settings, cfg, new() { ["user"] = resetUser });
                break;

            case "set_nickname":
                if (args.Length < 2) break;
                await _userService.SetNicknameAsync(args[0], args[1]);
                await Reply(settings, cfg, new() { ["user"] = args[0], ["value"] = args[1] });
                break;

            case "set_sticky":
                if (args.Length == 0) break;
                var stickyOn = args[0].Equals("on", StringComparison.OrdinalIgnoreCase);
                _settingsRepo.Update(s => s.StickyRandomVoice = stickyOn);
                await Reply(settings, cfg, new() { ["state"] = stickyOn ? "enabled" : "disabled" });
                break;

            case "assign_last":
                if (args.Length == 0) break;
                var (lastVoiceId, lastEngineId) = _queue.LastUsedVoice;
                if (!string.IsNullOrEmpty(lastVoiceId))
                {
                    await _userService.AssignLastVoiceAsync(args[0], lastVoiceId, lastEngineId);
                    await Reply(settings, cfg, new() { ["user"] = args[0] });
                }
                break;

            case "status":
                await Reply(settings, cfg, new()
                {
                    ["enabled"] = settings.Enabled ? "enabled" : "disabled",
                    ["mode"] = settings.Mode,
                    ["queue"] = _queue.Count.ToString(),
                });
                break;

            case "voices":
                var voices = await _voicePool.GetAllAsync();
                var summary = voices.Count == 0
                    ? "none"
                    : string.Join(", ", voices.GroupBy(v => v.EngineId).Select(g => $"{_engineRegistry.GetDisplayName(g.Key)}: {g.Count()}"));
                await Reply(settings, cfg, new() { ["count"] = voices.Count.ToString(), ["summary"] = summary });
                break;

            case "commands":
                var cmds = _customCommandRepo.GetAll().Where(c => c.Enabled).ToList();
                await Reply(settings, cfg, new()
                {
                    ["list"] = cmds.Count == 0 ? "none" : string.Join(", ", cmds.Select(c => c.Trigger)),
                });
                break;

            case "about":
                await Reply(settings, cfg, new() { ["instance"] = settings.InstanceName });
                break;
        }
    }
}
