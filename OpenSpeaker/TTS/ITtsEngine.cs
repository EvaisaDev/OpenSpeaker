namespace OpenSpeaker.TTS;

public interface ITtsEngine : IDisposable
{
    string EngineId { get; }
    bool IsConfigured { get; }
    Task<AudioData> SynthesizeAsync(string text, string voiceId, SynthParams parameters);
    Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync();
    void Configure(string configJson);
    IReadOnlyList<EngineParameterDef> GetParameters();
}

public class VoiceInfo
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Locale { get; init; } = string.Empty;
    public string Gender { get; init; } = string.Empty;
    public string EngineId { get; init; } = string.Empty;
}
