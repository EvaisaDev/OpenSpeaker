using OpenSpeaker.TTS;
namespace OpenSpeaker.Extensions;

public class LuaTtsEngine : ITtsEngine
{
    private readonly LuaExtension _extension;

    public string EngineId { get; }
    public string DisplayName { get; }
    public IReadOnlyList<ExtAuthField> AuthFields { get; }
    public bool IsConfigured => true;

    internal LuaTtsEngine(LuaExtension extension, string engineId, string displayName, IReadOnlyList<ExtAuthField> authFields)
    {
        _extension = extension;
        EngineId = engineId;
        DisplayName = displayName;
        AuthFields = authFields;
    }

    public void Configure(string configJson) => _extension.SetAuth(EngineId, configJson);

    public Task<AudioData> SynthesizeAsync(string text, string voiceId, SynthParams parameters) =>
        _extension.SynthesizeAsync(EngineId, text, voiceId, parameters);

    public Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync() =>
        _extension.GetVoicesAsync(EngineId);

    public IReadOnlyList<EngineParameterDef> GetParameters() => Array.Empty<EngineParameterDef>();

    public void Dispose() { }
}
