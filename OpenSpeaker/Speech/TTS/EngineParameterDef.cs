using System.Collections.Generic;
using System.Globalization;
namespace OpenSpeaker.TTS;

public enum EngineParameterType { Slider, ComboBox }

public sealed class EngineParameterDef
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public EngineParameterType Type { get; init; }
    public double Min { get; init; }
    public double Max { get; init; } = 1;
    public double Step { get; init; } = 1;
    public string Default { get; init; } = "0";
    public IReadOnlyList<string>? Options { get; init; }

    public static EngineParameterDef Slider(string key, string label, double min, double max, double step, double def) =>
        new() { Key = key, Label = label, Type = EngineParameterType.Slider, Min = min, Max = max, Step = step, Default = def.ToString(CultureInfo.InvariantCulture) };

    public static EngineParameterDef Combo(string key, string label, IReadOnlyList<string> options, string def) =>
        new() { Key = key, Label = label, Type = EngineParameterType.ComboBox, Options = options, Default = def };
}
