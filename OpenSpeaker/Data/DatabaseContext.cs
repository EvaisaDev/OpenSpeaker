using LiteDB;
using OpenSpeaker.Models;
using OpenSpeaker.TTS;
namespace OpenSpeaker.Data;

public class DatabaseContext : IDisposable
{
    private readonly LiteDatabase _db;

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

    public ILiteCollection<T> Collection<T>(string name) => _db.GetCollection<T>(name);

    public ILiteCollection<AppSettings> Settings => _db.GetCollection<AppSettings>("settings");
    public ILiteCollection<VoiceAlias> VoiceAliases => _db.GetCollection<VoiceAlias>("aliases");
    public ILiteCollection<UserRecord> Users => _db.GetCollection<UserRecord>("users");
    public ILiteCollection<EventConfig> Events => _db.GetCollection<EventConfig>("events");
    public ILiteCollection<CustomCommand> CustomCommands => _db.GetCollection<CustomCommand>("customcommands");
    public ILiteCollection<IgnoreProfile> IgnoreProfiles => _db.GetCollection<IgnoreProfile>("ignoreprofiles");
    public ILiteCollection<VoiceGateProfile> VoiceGateProfiles => _db.GetCollection<VoiceGateProfile>("voicegateprofiles");
    public ILiteCollection<BadWordEntry> BadWords => _db.GetCollection<BadWordEntry>("badwords");
    public ILiteCollection<RegexReplacement> RegexReplacements => _db.GetCollection<RegexReplacement>("regexreplacements");
    public ILiteCollection<EngineConfig> EngineConfigs => _db.GetCollection<EngineConfig>("engineconfigs");
    public ILiteCollection<TwitchAccountInfo> TwitchAccounts => _db.GetCollection<TwitchAccountInfo>("twitchaccounts");
    public ILiteCollection<ChannelReward> ChannelRewards => _db.GetCollection<ChannelReward>("channelrewards");
    public ILiteCollection<CustomApiDefinition> CustomApis => _db.GetCollection<CustomApiDefinition>("customapis");

    public void Dispose() => _db.Dispose();
}
