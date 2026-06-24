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

public interface IVoiceSearchEngine
{
    Task<IReadOnlyList<VoiceInfo>> TopVoicesAsync(int limit);
    Task<IReadOnlyList<VoiceInfo>> SearchVoicesAsync(string query, int limit);
    Task<VoiceInfo?> ResolveVoiceAsync(string id);
}

public class VoiceInfo
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Locale { get; init; } = string.Empty;
    public string Gender { get; init; } = string.Empty;
    public string EngineId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public int LikeCount { get; init; }
    public override string ToString() => !string.IsNullOrEmpty(DisplayName) ? DisplayName : Name;
}
