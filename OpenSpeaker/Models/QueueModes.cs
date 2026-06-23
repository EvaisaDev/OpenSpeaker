namespace OpenSpeaker.Models;
public static class QueueModes
{
    public const string Sequential    = "Sequential";
    public const string Simultaneous  = "Simultaneous";
    public const string PreGenerated  = "Pre-generated";

    public static readonly IReadOnlyList<string> All = [Sequential, Simultaneous, PreGenerated];
}
