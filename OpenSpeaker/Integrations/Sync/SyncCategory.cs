namespace OpenSpeaker.Sync;

public record SyncCategory(string Key, string LabelKey, string[] Collections)
{
    public const string OtherKey = "other";

    public static readonly SyncCategory[] All =
    {
        new("settings", "Sync.Cat.Settings", new[] { "settings" }),
        new("aliases", "Sync.Cat.VoiceAliases", new[] { "aliases" }),
        new("users", "Sync.Cat.Users", new[] { "users" }),
        new("events", "Sync.Cat.Events", new[] { "events" }),
        new("commands", "Sync.Cat.Commands", new[] { "customcommands" }),
        new("rewards", "Sync.Cat.Rewards", new[] { "channelrewards" }),
        new("engines", "Sync.Cat.SpeechEngines", new[] { "engineconfigs", "customapis" }),
        new("filters", "Sync.Cat.Filters", new[] { "regexreplacements", "badwords", "voiceswitches" }),
        new("ignoredvoices", "Sync.Cat.IgnoredVoices", new[] { "ignoreprofiles" }),
        new("voicegate", "Sync.Cat.VoiceGate", new[] { "voicegateprofiles" }),
        new("twitch", "Sync.Cat.TwitchAccounts", new[] { "twitchaccounts" }),
        new("extensions", "Sync.Cat.Extensions", new[] { "extensionconfigs", "extensionsettings", "extensiondata" }),
        new(OtherKey, "Sync.Cat.Other", Array.Empty<string>()),
    };

    private static readonly HashSet<string> KnownCollections =
        new(All.SelectMany(c => c.Collections), StringComparer.OrdinalIgnoreCase);

    public static Func<string, bool> BuildFilter(IEnumerable<string> selectedKeys)
    {
        var keys = new HashSet<string>(selectedKeys, StringComparer.OrdinalIgnoreCase);
        var collections = new HashSet<string>(
            All.Where(c => keys.Contains(c.Key)).SelectMany(c => c.Collections),
            StringComparer.OrdinalIgnoreCase);
        var includeOther = keys.Contains(OtherKey);
        return name => collections.Contains(name) || (includeOther && !KnownCollections.Contains(name));
    }
}
