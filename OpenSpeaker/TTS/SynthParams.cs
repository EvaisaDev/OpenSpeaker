using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
namespace OpenSpeaker.TTS;

public sealed class SynthParams
{
    private readonly IReadOnlyDictionary<string, string> _d;

    public static SynthParams Empty { get; } = new();

    public SynthParams() => _d = new Dictionary<string, string>();
    public SynthParams(IReadOnlyDictionary<string, string> d) => _d = d;

    public double Dbl(string key, double def = 0) =>
        _d.TryGetValue(key, out var s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : def;

    public int Int(string key, int def = 0) => (int)Math.Round(Dbl(key, def));

    public string Str(string key, string def = "") =>
        _d.TryGetValue(key, out var s) ? s : def;

    public static SynthParams FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}") return Empty;
        try
        {
            var d = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            return new SynthParams(d);
        }
        catch { return Empty; }
    }
}
