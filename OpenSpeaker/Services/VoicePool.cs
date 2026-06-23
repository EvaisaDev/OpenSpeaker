using OpenSpeaker.Data;
using OpenSpeaker.TTS;
namespace OpenSpeaker.Services;

public class VoicePool
{
    private readonly TtsEngineRegistry _registry;
    private readonly DatabaseContext _db;
    private List<(string EngineId, VoiceInfo Voice)>? _cache;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Random _rng = new();

    public VoicePool(TtsEngineRegistry registry, DatabaseContext db)
    {
        _registry = registry;
        _db = db;
    }

    public async Task<(string EngineId, VoiceInfo Voice)?> GetRandomAsync()
    {
        var voices = await GetAllAsync();
        var excluded = GetActiveExcludedIds();
        var available = excluded.Count == 0
            ? voices
            : voices.Where(v => !excluded.Contains($"{v.EngineId}::{v.Voice.Id}")).ToList();
        if (available.Count == 0) return null;
        return available[_rng.Next(available.Count)];
    }

    private HashSet<string> GetActiveExcludedIds()
    {
        var profile = _db.IgnoreProfiles.FindOne(p => p.IsActive);
        return profile?.ExcludedVoiceIds is { Count: > 0 } ids
            ? new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>();
    }

    public async Task<IReadOnlyList<(string EngineId, VoiceInfo Voice)>> GetAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_cache != null) return _cache;
            var allEngines = _registry.GetEnabledEngineIds()
                .Select(id => (id, engine: _registry.GetEngine(id)))
                .Where(x => x.engine != null)
                .ToList();

            var nativeEngines = allEngines.Where(x => x.engine is not OpenSpeaker.Extensions.LuaTtsEngine).ToList();
            var luaEngines    = allEngines.Where(x => x.engine is OpenSpeaker.Extensions.LuaTtsEngine).ToList();

            var nativeTasks = nativeEngines.Select(async x =>
            {
                try { return (x.id, voices: await x.engine!.GetVoicesAsync()); }
                catch { return (x.id, voices: (IReadOnlyList<VoiceInfo>)Array.Empty<VoiceInfo>()); }
            });
            var nativeResults = await Task.WhenAll(nativeTasks);

            var luaResults = new List<(string id, IReadOnlyList<VoiceInfo> voices)>();
            foreach (var x in luaEngines)
            {
                try { luaResults.Add((x.id, await x.engine!.GetVoicesAsync())); }
                catch { luaResults.Add((x.id, Array.Empty<VoiceInfo>())); }
            }

            var result = nativeResults.Concat(luaResults)
                .SelectMany(r => r.voices.Select(v => (r.id, v)))
                .ToList();
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
