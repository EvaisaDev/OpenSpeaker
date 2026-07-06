using LiteDB;
using OpenSpeaker.Models;
using OpenSpeaker.TTS;
namespace OpenSpeaker.Data;

public class DatabaseContext : IDisposable
{
    private readonly LiteDatabase _db;
    private readonly object _gate = new();

    public DatabaseContext(string dbPath)
    {
        _db = new LiteDatabase(dbPath);
        Bootstrap();
    }

    private void Bootstrap()
    {
        var settings = _db.GetCollection<AppSettings>("settings");
        if (settings.Count() == 0)
            settings.Insert(new AppSettings());
    }

    private ILiteCollection<T> Synchronized<T>(ILiteCollection<T> collection) =>
        new SynchronizedLiteCollection<T>(collection, _gate);

    public ILiteCollection<T> Collection<T>(string name) => Synchronized(_db.GetCollection<T>(name));

    public ILiteCollection<AppSettings> Settings => Synchronized(_db.GetCollection<AppSettings>("settings"));
    public ILiteCollection<VoiceAlias> VoiceAliases => Synchronized(_db.GetCollection<VoiceAlias>("aliases"));
    public ILiteCollection<UserRecord> Users => Synchronized(_db.GetCollection<UserRecord>("users"));
    public ILiteCollection<EventConfig> Events => Synchronized(_db.GetCollection<EventConfig>("events"));
    public ILiteCollection<CustomCommand> CustomCommands => Synchronized(_db.GetCollection<CustomCommand>("customcommands"));
    public ILiteCollection<IgnoreProfile> IgnoreProfiles => Synchronized(_db.GetCollection<IgnoreProfile>("ignoreprofiles"));
    public ILiteCollection<VoiceGateProfile> VoiceGateProfiles => Synchronized(_db.GetCollection<VoiceGateProfile>("voicegateprofiles"));
    public ILiteCollection<BadWordEntry> BadWords => Synchronized(_db.GetCollection<BadWordEntry>("badwords"));
    public ILiteCollection<RegexReplacement> RegexReplacements => Synchronized(_db.GetCollection<RegexReplacement>("regexreplacements"));
    public ILiteCollection<EngineConfig> EngineConfigs => Synchronized(_db.GetCollection<EngineConfig>("engineconfigs"));
    public ILiteCollection<TwitchAccountInfo> TwitchAccounts => Synchronized(_db.GetCollection<TwitchAccountInfo>("twitchaccounts"));
    public ILiteCollection<ChannelReward> ChannelRewards => Synchronized(_db.GetCollection<ChannelReward>("channelrewards"));
    public ILiteCollection<CustomApiDefinition> CustomApis => Synchronized(_db.GetCollection<CustomApiDefinition>("customapis"));
    public ILiteCollection<ExtensionConfig> ExtensionConfigs => Synchronized(_db.GetCollection<ExtensionConfig>("extensionconfigs"));
    public ILiteCollection<ExtensionSettings> ExtensionSettings => Synchronized(_db.GetCollection<ExtensionSettings>("extensionsettings"));

    public ILiteCollection<BsonDocument> RawCollection(string name) => Synchronized(_db.GetCollection<BsonDocument>(name));
    public void DropCollection(string name) { lock (_gate) _db.DropCollection(name); }

    public void Dispose() { lock (_gate) _db.Dispose(); }
}
