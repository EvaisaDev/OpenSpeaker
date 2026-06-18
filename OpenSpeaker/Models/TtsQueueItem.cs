namespace OpenSpeaker.Models;
public class TtsQueueItem
{
    public string Text { get; set; } = string.Empty;
    public string VoiceAliasName { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public bool IsSilent { get; set; } = false;
    public bool ApplyBadWordFilter { get; set; } = true;
    public string? SourceEvent { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string StickyVoiceId { get; set; } = string.Empty;
    public string StickyVoiceEngineId { get; set; } = string.Empty;
}
