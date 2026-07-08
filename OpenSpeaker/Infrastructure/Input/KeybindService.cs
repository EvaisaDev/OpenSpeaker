using OpenSpeaker.Infrastructure.Logging;
namespace OpenSpeaker.Input;

public sealed class KeybindService : IDisposable
{
    private readonly GlobalKeyboardHook _hook;
    private readonly object _lock = new();
    private readonly HashSet<int> _live = new();
    private readonly List<KeyEvent> _scratch = new();

    private HashSet<int> _startState = new();
    private HashSet<int> _endState = new();
    private KeyEvent[] _events = Array.Empty<KeyEvent>();
    private volatile bool _resyncRequested;

    public KeybindService(IAppLogger? logger = null)
    {
        _hook = new GlobalKeyboardHook(logger);
        _hook.Reinstalled += () => _resyncRequested = true;
    }

    public bool IsInstalled => _hook.IsInstalled;

    public void Install() => _hook.Install();

    public void Tick()
    {
        _hook.EnsureInstalled();

        if (_resyncRequested)
        {
            _resyncRequested = false;
            _live.Clear();
        }

        _scratch.Clear();
        _scratch.AddRange(_hook.DrainEvents());

        var start = new HashSet<int>(_live);
        foreach (var e in _scratch)
        {
            if (e.IsDown) _live.Add(e.VirtualKey);
            else _live.Remove(e.VirtualKey);
        }

        AppendSyntheticReleases();

        var end = new HashSet<int>(_live);
        var events = _scratch.Count == 0 ? Array.Empty<KeyEvent>() : _scratch.ToArray();

        lock (_lock)
        {
            _startState = start;
            _endState = end;
            _events = events;
        }
    }

    private void AppendSyntheticReleases()
    {
        if (_live.Count == 0) return;
        List<int>? stuck = null;
        foreach (var vk in _live)
        {
            if (GlobalKeyboardHook.IsPhysicallyDown(vk)) continue;
            (stuck ??= new List<int>()).Add(vk);
        }
        if (stuck is null) return;
        foreach (var vk in stuck)
        {
            _live.Remove(vk);
            _scratch.Add(new KeyEvent(vk, false));
        }
    }

    public bool IsHeld(KeyChord chord)
    {
        if (chord.IsEmpty) return false;
        lock (_lock) return chord.IsSatisfiedBy(_endState);
    }

    public bool WasPressed(KeyChord chord) => HasTransitionTo(chord, true);

    public bool WasReleased(KeyChord chord) => HasTransitionTo(chord, false);

    private bool HasTransitionTo(KeyChord chord, bool target)
    {
        if (chord.IsEmpty) return false;

        HashSet<int> state;
        KeyEvent[] events;
        lock (_lock)
        {
            if (_events.Length == 0) return false;
            state = new HashSet<int>(_startState);
            events = _events;
        }

        var satisfied = chord.IsSatisfiedBy(state);
        foreach (var e in events)
        {
            var changed = e.IsDown ? state.Add(e.VirtualKey) : state.Remove(e.VirtualKey);
            if (!changed) continue;

            var now = chord.IsSatisfiedBy(state);
            if (now == satisfied) continue;
            satisfied = now;
            if (now == target) return true;
        }
        return false;
    }

    public void Dispose() => _hook.Dispose();
}
