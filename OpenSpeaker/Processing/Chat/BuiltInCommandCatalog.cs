using OpenSpeaker.Models;
namespace OpenSpeaker.Chat;

public sealed record BuiltInCommandInfo(
    string Id,
    string DefaultKeyword,
    string DefaultReply,
    string[] DefaultRoles,
    string Placeholders,
    string Description);

public static class BuiltInCommandCatalog
{
    private static readonly string[] Mod = { UserRoles.Moderator };
    private static readonly string[] All = { UserRoles.Everyone };

    public static readonly IReadOnlyList<BuiltInCommandInfo> Commands = new[]
    {
        new BuiltInCommandInfo("pause",        "pause",         "TTS paused.",                              Mod, "", "Pause the TTS queue."),
        new BuiltInCommandInfo("resume",       "resume",        "TTS resumed.",                             Mod, "", "Resume the TTS queue."),
        new BuiltInCommandInfo("clear",        "clear",         "TTS queue cleared.",                       Mod, "", "Clear the TTS queue."),
        new BuiltInCommandInfo("stop",         "stop",          "TTS stopped.",                             Mod, "", "Stop the current TTS playback."),
        new BuiltInCommandInfo("mode",         "mode",          "TTS mode set to {mode}.",                  Mod, "{mode}", "Set TTS mode (arg: all | command)."),
        new BuiltInCommandInfo("enable",       "on, enable",    "TTS enabled.",                             Mod, "", "Enable the bot."),
        new BuiltInCommandInfo("disable",      "off, disable",  "TTS disabled.",                            Mod, "", "Disable the bot."),
        new BuiltInCommandInfo("events",       "events",        "TTS events {state}.",                      Mod, "{state}", "Toggle event TTS (arg: on | off)."),
        new BuiltInCommandInfo("ignore",       "ignore",        "{user} {action} the ignore list.",         Mod, "{user}, {action}", "Add/remove a user from the ignore list (args: add|remove <user>)."),
        new BuiltInCommandInfo("ignored",      "ignored",       "Ignored: {users}",                         All, "{users}, {count}", "List ignored users."),
        new BuiltInCommandInfo("reg",          "reg",           "{user} {action} a regular.",               Mod, "{user}, {action}", "Add/remove a regular (args: add|remove <user>)."),
        new BuiltInCommandInfo("random_reset", "random reset",  "{user}'s random voice has been reset.",    All, "{user}", "Reset a random voice (self, or any user for mods)."),
        new BuiltInCommandInfo("set_nickname", "set nickname",  "{user}'s nickname set to {value}.",        Mod, "{user}, {value}", "Set a user's nickname (args: <user> <nickname>)."),
        new BuiltInCommandInfo("set_sticky",   "set sticky",    "Sticky random voice {state}.",             Mod, "{state}", "Toggle sticky random voice (arg: on | off)."),
        new BuiltInCommandInfo("assign_last",  "assign last",   "Assigned last used voice to {user}.",      Mod, "{user}", "Assign the last used voice to a user (arg: <user>)."),
        new BuiltInCommandInfo("status",       "status",        "OpenSpeaker: {enabled}, mode: {mode}, queue: {queue} item(s).", All, "{enabled}, {mode}, {queue}", "Report bot status."),
        new BuiltInCommandInfo("voices",       "voices",        "Voices ({count} total) - {summary}",       All, "{count}, {summary}", "Report loaded voice counts."),
        new BuiltInCommandInfo("commands",     "commands",      "Commands: {list}",                         All, "{list}", "List enabled custom commands."),
        new BuiltInCommandInfo("about",        "about",         "OpenSpeaker, Instance: {instance}",        All, "{instance}", "Show instance info."),
    };

    private static readonly Dictionary<string, BuiltInCommandInfo> ById =
        Commands.ToDictionary(c => c.Id, StringComparer.Ordinal);

    public static BuiltInCommandInfo? Info(string id) => ById.GetValueOrDefault(id);

    public static BuiltInCommandConfig Default(BuiltInCommandInfo info) => new()
    {
        Id = info.Id,
        Keyword = info.DefaultKeyword,
        Reply = info.DefaultReply,
        AllowedRoles = info.DefaultRoles.ToList(),
        Enabled = true,
    };

    public static List<BuiltInCommandConfig> Resolve(AppSettings settings)
    {
        var saved = settings.BuiltInCommands.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var result = new List<BuiltInCommandConfig>();
        foreach (var info in Commands)
        {
            if (saved.TryGetValue(info.Id, out var cfg))
            {
                if (string.IsNullOrWhiteSpace(cfg.Keyword)) cfg.Keyword = info.DefaultKeyword;
                if (cfg.AllowedRoles == null! || cfg.AllowedRoles.Count == 0) cfg.AllowedRoles = info.DefaultRoles.ToList();
                result.Add(cfg);
            }
            else
            {
                result.Add(Default(info));
            }
        }
        return result;
    }
}
