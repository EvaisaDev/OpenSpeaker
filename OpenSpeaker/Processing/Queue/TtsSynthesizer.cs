using OpenSpeaker.Audio;
using OpenSpeaker.Data;
using OpenSpeaker.Infrastructure.Logging;
using OpenSpeaker.Models;
using OpenSpeaker.TTS;
using OpenSpeaker.Users;
namespace OpenSpeaker.Queue;

public record SynthesisResult(TtsQueueItem Item, AudioData Audio, string DeviceId, string? SavedPath);

public class TtsSynthesizer
{
    private readonly VoiceResolver _resolver;
    private readonly WavFileSaver _wavSaver;
    private readonly SettingsRepository _settingsRepo;
    private readonly UserService _userService;
    private readonly IAppLogger? _logger;
    private (string VoiceId, string EngineId) _lastUsedVoice;

    public (string VoiceId, string EngineId) LastUsedVoice => _lastUsedVoice;

    public TtsSynthesizer(
        VoiceResolver resolver,
        WavFileSaver wavSaver,
        SettingsRepository settingsRepo,
        UserService userService,
        IAppLogger? logger = null)
    {
        _resolver = resolver;
        _wavSaver = wavSaver;
        _settingsRepo = settingsRepo;
        _userService = userService;
        _logger = logger;
    }

    public async Task<SynthesisResult?> SynthesizeAsync(TtsQueueItem item, Action onStarted)
    {
        var settings = _settingsRepo.GetSettings();

        var resolved = _resolver.Resolve(item, settings);
        if (resolved == null)
        {
            _logger?.Info($"QUEUE :: No voice resolved for '{item.Text}' (alias='{item.VoiceAliasName}') - dropped");
            return null;
        }

        var engine = resolved.Engine;
        var voiceId = resolved.VoiceId;
        var synthParams = resolved.Params;
        var deviceId = resolved.DeviceId;
        var aliasName = resolved.AliasName;

        _lastUsedVoice = (voiceId, engine.EngineId);
        if (!string.IsNullOrEmpty(item.UserId) && !string.IsNullOrEmpty(voiceId))
            _userService.AddPastVoiceAsync(item.UserId, voiceId, engine.EngineId).Forget(_logger, "AddPastVoice");

        _logger?.Info($"QUEUE :: Processing '{item.Text}' engine={engine.EngineId} voiceId='{voiceId}' device='{deviceId}'");
        onStarted();

        try
        {
            var text = resolved.LowercaseText ? item.Text.ToLowerInvariant() : item.Text;
            var audio = await engine.SynthesizeAsync(text, voiceId, synthParams);
            _logger?.Info($"QUEUE :: Synthesis done. IsEmpty={audio.IsEmpty}");
            if (audio.IsEmpty) return null;

            audio = AudioGain.Apply(audio, resolved.Volume);

            string? savedPath = null;
            if (settings.SaveTts && !string.IsNullOrEmpty(settings.SaveTtsFolder))
            {
                var paramValues = engine.GetParameters()
                    .Select(p => synthParams.Str(p.Key, p.Default))
                    .ToList();
                savedPath = _wavSaver.Save(audio, settings.SaveTtsFolder,
                    paramValues, aliasName,
                    string.IsNullOrEmpty(item.Username) ? null : item.Username);
            }

            return new SynthesisResult(item, audio, deviceId, savedPath);
        }
        catch (Exception ex)
        {
            _logger?.Error($"TTS synthesis failed for engine {engine.EngineId}: {ex.Message}");
            return null;
        }
    }
}
