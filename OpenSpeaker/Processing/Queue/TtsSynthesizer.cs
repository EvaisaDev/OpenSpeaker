using OpenSpeaker.Audio;
using OpenSpeaker.Data;
using OpenSpeaker.Extensions;
using OpenSpeaker.Infrastructure.Logging;
using OpenSpeaker.Models;
using OpenSpeaker.Text;
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
    private readonly ExtensionManager? _extensions;
	private readonly VoiceSwitchParser? _switchParser;
    private readonly IAppLogger? _logger;
    private (string VoiceId, string EngineId) _lastUsedVoice;

    public (string VoiceId, string EngineId) LastUsedVoice => _lastUsedVoice;

    public TtsSynthesizer(
        VoiceResolver resolver,
        WavFileSaver wavSaver,
        SettingsRepository settingsRepo,
        UserService userService,
        ExtensionManager? extensions = null,
        IAppLogger? logger = null,
		VoiceSwitchParser? switchParser = null)
    {
        _resolver = resolver;
        _wavSaver = wavSaver;
        _settingsRepo = settingsRepo;
        _userService = userService;
        _extensions = extensions;
        _logger = logger;
		_switchParser = switchParser;
    }

    public async Task<SynthesisResult?> SynthesizeAsync(TtsQueueItem item, Action onStarted, CancellationToken cancellationToken = default)
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
		var segments = BuildSegments(item, resolved, settings);
		if (segments.Count > 1)
			_logger?.Info($"QUEUE :: Voice switches split message into {segments.Count} segments: {string.Join(" | ", segments.Select(s => $"[{s.Voice.AliasName}] {s.Text}"))}");
        onStarted();

        try
        {
			var clips = new List<AudioData>();
			foreach (var segment in segments)
			{
				var clip = await SynthesizeSegmentAsync(item, segment.Voice, segment.Text, cancellationToken);
				if (!clip.IsEmpty) clips.Add(clip);
			}
			_logger?.Info($"QUEUE :: Synthesis done. Clips={clips.Count}");

			var audio = AudioMerger.Concat(clips);
			if (audio.IsEmpty) return null;

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
        catch (OperationCanceledException)
        {
            _logger?.Info($"QUEUE :: Synthesis cancelled for '{item.Text}'");
            return null;
        }
        catch (Exception ex)
        {
            _logger?.Error($"TTS synthesis failed for engine {engine.EngineId}: {ex.Message}");
            return null;
        }
    }

	private List<(ResolvedVoice Voice, string Text)> BuildSegments(TtsQueueItem item, ResolvedVoice initial, AppSettings settings)
	{
		if (_switchParser == null)
			return new List<(ResolvedVoice, string)> { (initial, item.Text) };

		return _switchParser.Split(item.Text, item.Username)
			.Select(s => (s.AliasName == null ? initial : _resolver.ResolveAlias(s.AliasName, settings), s.Text))
			.ToList();
	}

	private async Task<AudioData> SynthesizeSegmentAsync(TtsQueueItem item, ResolvedVoice voice, string text, CancellationToken cancellationToken)
	{
		var input = voice.LowercaseText ? text.ToLowerInvariant() : text;
		var audio = await voice.Engine.SynthesizeAsync(input, voice.VoiceId, voice.Params).WaitAsync(cancellationToken);
		if (audio.IsEmpty) return audio;

		if (_extensions is { HasTransformAudioHooks: true })
		{
			audio = await _extensions.TransformAudioAsync(item.UserId, item.Username, voice.AliasName ?? string.Empty, audio).WaitAsync(cancellationToken);
			if (audio.IsEmpty) return audio;
		}

		return AudioGain.Apply(audio, voice.Volume);
	}
}
