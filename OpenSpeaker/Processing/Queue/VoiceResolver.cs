using OpenSpeaker.Data;
using OpenSpeaker.Models;
using OpenSpeaker.TTS;
namespace OpenSpeaker.Queue;

public record ResolvedVoice(ITtsEngine Engine, string VoiceId, SynthParams Params, string DeviceId, string AliasName);

public class VoiceResolver
{
    private readonly TtsEngineRegistry _engineRegistry;
    private readonly VoiceAliasRepository _aliasRepo;

    public VoiceResolver(TtsEngineRegistry engineRegistry, VoiceAliasRepository aliasRepo)
    {
        _engineRegistry = engineRegistry;
        _aliasRepo = aliasRepo;
    }

    public ResolvedVoice? Resolve(TtsQueueItem item, AppSettings settings)
    {
        if (!string.IsNullOrEmpty(item.StickyVoiceEngineId))
        {
            var stickyEngine = _engineRegistry.GetEngine(item.StickyVoiceEngineId) ?? _engineRegistry.GetDefaultEngine();
            return new ResolvedVoice(stickyEngine, item.StickyVoiceId, SynthParams.Empty, settings.AudioOutputDeviceId, item.VoiceAliasName);
        }

        var alias = _aliasRepo.GetByName(item.VoiceAliasName)
            ?? _aliasRepo.GetByName(settings.DefaultVoiceAlias);
        if (alias == null || string.IsNullOrEmpty(alias.VoiceId))
            return new ResolvedVoice(_engineRegistry.GetDefaultEngine(), string.Empty, SynthParams.Empty, settings.AudioOutputDeviceId, item.VoiceAliasName);

        var engine = _engineRegistry.GetEngine(alias.EngineId) ?? _engineRegistry.GetDefaultEngine();
        var deviceId = !string.IsNullOrEmpty(alias.OutputDeviceId) ? alias.OutputDeviceId : settings.AudioOutputDeviceId;
        var aliasName = !string.IsNullOrEmpty(alias.Name) ? alias.Name : item.VoiceAliasName;
        return new ResolvedVoice(engine, alias.VoiceId, SynthParams.FromJson(alias.EngineParamsJson), deviceId, aliasName);
    }
}
