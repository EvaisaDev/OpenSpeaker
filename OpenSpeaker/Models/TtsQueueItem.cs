namespace OpenSpeaker.Models;
public class TtsQueueItem
{
    public string SpeechId { get; set; } = Guid.NewGuid().ToString();
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
    public string Text { get; set; } = string.Empty;
    public string VoiceAliasName { get; set; } = string.Empty;
    public bool IsSilent { get; set; } = false;
    public string? SourceEvent { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string StickyVoiceId { get; set; } = string.Empty;
    public string StickyVoiceEngineId { get; set; } = string.Empty;
}
