using System.Text.RegularExpressions;
using OpenSpeaker.Data;
using OpenSpeaker.Models;
namespace OpenSpeaker.Text;

public record VoiceSwitchSegment(string? AliasName, string Text);

public class VoiceSwitchParser
{
	private readonly VoiceSwitchRepository _repo;

	public VoiceSwitchParser(VoiceSwitchRepository repo) { _repo = repo; }

	public List<VoiceSwitchSegment> Split(string text, string? username) =>
		Split(text, username, _repo.GetAll());

	public static List<VoiceSwitchSegment> Split(string text, string? username, IEnumerable<VoiceSwitch> switches)
	{
		var markers = BuildMarkerMap(username, switches);
		var regex = BuildMarkerRegex(markers);
		if (regex == null || string.IsNullOrEmpty(text))
			return new List<VoiceSwitchSegment> { new(null, text) };

		var segments = new List<VoiceSwitchSegment>();
		string? currentAlias = null;
		var pos = 0;
		foreach (Match m in regex.Matches(text))
		{
			AddSegment(segments, currentAlias, text[pos..m.Index]);
			currentAlias = markers[m.Value];
			pos = m.Index + m.Length;
		}
		AddSegment(segments, currentAlias, text[pos..]);
		if (segments.Count == 0 && pos == 0)
			segments.Add(new VoiceSwitchSegment(null, text));
		return segments;
	}

	public bool StartsWithMarker(string text, string? username)
	{
		var trimmed = text.TrimStart();
		return BuildMarkerMap(username, _repo.GetAll()).Keys
			.Any(k => trimmed.StartsWith(k, StringComparison.OrdinalIgnoreCase));
	}

	private static readonly Regex PlaceholderRegex = new(@"vswmk(\d+)q", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	public string ProtectMarkers(string text, string? username, out List<string> protectedMarkers)
	{
		var found = new List<string>();
		protectedMarkers = found;
		var regex = BuildMarkerRegex(BuildMarkerMap(username, _repo.GetAll()));
		if (regex == null || string.IsNullOrEmpty(text)) return text;
		return regex.Replace(text, m =>
		{
			found.Add(m.Value);
			return $" vswmk{found.Count - 1}q ";
		});
	}

	public static string RestoreMarkers(string text, IReadOnlyList<string> protectedMarkers)
	{
		if (protectedMarkers.Count == 0) return text;
		return PlaceholderRegex.Replace(text, m =>
			int.TryParse(m.Groups[1].Value, out var i) && i < protectedMarkers.Count ? protectedMarkers[i] : string.Empty);
	}

	private static Regex? BuildMarkerRegex(Dictionary<string, string> markers)
	{
		if (markers.Count == 0) return null;
		var pattern = string.Join("|", markers.Keys
			.OrderByDescending(k => k.Length)
			.Select(Regex.Escape));
		return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
	}

	private static Dictionary<string, string> BuildMarkerMap(string? username, IEnumerable<VoiceSwitch> switches)
	{
		var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		var active = switches
			.Where(s => s.Enabled && !string.IsNullOrEmpty(s.Marker) && !string.IsNullOrWhiteSpace(s.AliasName))
			.ToList();

		foreach (var s in active.Where(s => s.IsGlobal))
			map[s.Marker] = s.AliasName;

		if (!string.IsNullOrWhiteSpace(username))
			foreach (var s in active.Where(s => !s.IsGlobal && string.Equals(s.Username.Trim(), username.Trim(), StringComparison.OrdinalIgnoreCase)))
				map[s.Marker] = s.AliasName;

		return map;
	}

	private static void AddSegment(List<VoiceSwitchSegment> segments, string? alias, string text)
	{
		var trimmed = text.Trim();
		if (!trimmed.Any(char.IsLetterOrDigit)) return;
		if (segments.Count > 0 && segments[^1].AliasName == alias)
			segments[^1] = segments[^1] with { Text = segments[^1].Text + " " + trimmed };
		else
			segments.Add(new VoiceSwitchSegment(alias, trimmed));
	}
}
