using OpenSpeaker.TTS;
namespace OpenSpeaker.Services;

public class VoicePool
{
    private readonly TtsEngineRegistry _registry;
    private List<(string EngineId, VoiceInfo Voice)>? _cache;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Random _rng = new();

    public VoicePool(TtsEngineRegistry registry) => _registry = registry;

    public async Task<(string EngineId, VoiceInfo Voice)?> GetRandomAsync()
    {
        var voices = await GetAllAsync();
        if (voices.Count == 0) return null;
        return voices[_rng.Next(voices.Count)];
    }

    public async Task<IReadOnlyList<(string EngineId, VoiceInfo Voice)>> GetAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_cache != null) return _cache;
            var result = new List<(string, VoiceInfo)>();
            foreach (var engineId in _registry.GetEnabledEngineIds())
            {
                var engine = _registry.GetEngine(engineId);
                if (engine == null) continue;
                try
                {
                    var voices = await engine.GetVoicesAsync();
                    foreach (var v in voices)
                        result.Add((engineId, v));
                }
                catch { }
            }
            _cache = result;
            return result;
        }
        finally { _lock.Release(); }
    }

    public void Invalidate()
    {
        _lock.Wait();
        try { _cache = null; }
        finally { _lock.Release(); }
    }
}
