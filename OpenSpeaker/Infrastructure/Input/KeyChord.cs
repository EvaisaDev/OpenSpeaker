namespace OpenSpeaker.Input;

public sealed class KeyChord
{
    public static readonly KeyChord Empty = new(Array.Empty<int[]>());

    private readonly int[][] _groups;

    public KeyChord(int[][] groups) => _groups = groups;

    public bool IsEmpty => _groups.Length == 0;

    public IReadOnlyList<int[]> Groups => _groups;

    public bool IsSatisfiedBy(HashSet<int> down)
    {
        if (_groups.Length == 0) return false;
        foreach (var group in _groups)
        {
            var hit = false;
            foreach (var vk in group)
            {
                if (!down.Contains(vk)) continue;
                hit = true;
                break;
            }
            if (!hit) return false;
        }
        return true;
    }
}
