namespace OpenSpeaker.Events;
public interface IEventProcessor
{
    Task ProcessAsync(string eventType, Dictionary<string, string> variables, string? aliasOverride = null);
}
