using System.Collections.Concurrent;
using System.Windows.Input;
namespace OpenSpeaker.Input;

public static class KeyNameMap
{
    private static readonly ConcurrentDictionary<string, KeyChord> ChordCache = new(StringComparer.OrdinalIgnoreCase);

    public static KeyChord ResolveChord(string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return KeyChord.Empty;
        return ChordCache.GetOrAdd(spec.Trim(), BuildChord);
    }

    private static KeyChord BuildChord(string spec)
    {
        var groups = new List<int[]>();
        foreach (var token in SplitChord(spec))
        {
            var vks = Resolve(token);
            if (vks.Length == 0) return KeyChord.Empty;
            if (groups.Any(g => g.SequenceEqual(vks))) continue;
            groups.Add(vks);
        }
        return groups.Count == 0 ? KeyChord.Empty : new KeyChord(groups.ToArray());
    }

    private static IEnumerable<string> SplitChord(string spec)
    {
        var tokens = spec.Split('+');
        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i].Trim();
            if (token.Length > 0)
            {
                yield return token;
                continue;
            }
            if (i + 1 < tokens.Length && tokens[i + 1].Trim().Length == 0)
            {
                i++;
                yield return "OemPlus";
            }
        }
    }

    public static int[] Resolve(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return Array.Empty<int>();
        name = name.Trim();
        switch (name.ToLowerInvariant())
        {
            case "ctrl":
            case "control": return new[] { 0xA2, 0xA3 };
            case "shift": return new[] { 0xA0, 0xA1 };
            case "alt": return new[] { 0xA4, 0xA5 };
            case "win":
            case "super":
            case "meta": return new[] { 0x5B, 0x5C };
            case "lctrl":
            case "leftctrl": return new[] { 0xA2 };
            case "rctrl":
            case "rightctrl": return new[] { 0xA3 };
            case "lshift":
            case "leftshift": return new[] { 0xA0 };
            case "rshift":
            case "rightshift": return new[] { 0xA1 };
            case "lalt":
            case "leftalt": return new[] { 0xA4 };
            case "ralt":
            case "rightalt": return new[] { 0xA5 };
            case "plus": return new[] { 0xBB };
        }

        if (name.Length == 1 && name[0] >= '0' && name[0] <= '9')
            return new[] { KeyInterop.VirtualKeyFromKey(Key.D0 + (name[0] - '0')) };

        if (int.TryParse(name, out _)) return Array.Empty<int>();

        if (Enum.TryParse<Key>(name, true, out var key) && key != Key.None)
        {
            var vk = KeyInterop.VirtualKeyFromKey(key);
            if (vk != 0) return new[] { vk };
        }
        return Array.Empty<int>();
    }

    public static string Display(Key key) => key switch
    {
        Key.LeftCtrl or Key.RightCtrl => "Ctrl",
        Key.LeftShift or Key.RightShift => "Shift",
        Key.LeftAlt or Key.RightAlt => "Alt",
        Key.LWin or Key.RWin => "Win",
        Key.OemPlus => "Plus",
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        _ => key.ToString()
    };
}
