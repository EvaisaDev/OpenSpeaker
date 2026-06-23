namespace OpenSpeaker.Import;

public record MigrationEngineConfig(
    string TargetEngineId,
    IReadOnlyDictionary<string, string> FieldMap
);

public record MigrationData(
    IReadOnlyDictionary<string, string> VoiceEngineRemap,
    IReadOnlyDictionary<string, MigrationEngineConfig> EngineConfigs
)
{
    public static readonly MigrationData Empty = new(
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, MigrationEngineConfig>(StringComparer.OrdinalIgnoreCase)
    );
}
