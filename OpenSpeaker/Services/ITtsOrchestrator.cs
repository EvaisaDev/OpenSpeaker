namespace OpenSpeaker.Services;
public interface ITtsOrchestrator
{
    Task<string?> SpeakAsync(string text, string voiceAliasName, bool applyBadWordFilter = true, bool silent = false, bool delay = false);
    void Pause();
    void Resume();
    void Clear();
    void Stop();
    bool IsEnabled { get; }
    void SetEnabled(bool enabled);
}
