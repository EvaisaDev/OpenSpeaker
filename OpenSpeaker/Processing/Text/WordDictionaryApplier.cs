using System.Text.RegularExpressions;
using OpenSpeaker.Data;
using OpenSpeaker.Models;
namespace OpenSpeaker.Text;

public class WordDictionaryApplier
{
	private readonly WordDictionaryRepository _repo;

	public WordDictionaryApplier(WordDictionaryRepository repo) { _repo = repo; }

	public string Apply(string text, string? aliasName) =>
		Apply(text, aliasName, _repo.GetAll());

	public static string Apply(string text, string? aliasName, IEnumerable<WordDictionaryEntry> entries)
	{
		var map = BuildReplacementMap(aliasName, entries);
		var regex = BuildMatchRegex(map);
		if (regex == null || string.IsNullOrEmpty(text))
			return text;

		return regex.Replace(text, m => map[m.Value]);
	}

	private static Regex? BuildMatchRegex(Dictionary<string, string> map)
	{
		if (map.Count == 0) return null;
		var pattern = string.Join("|", map.Keys
			.OrderByDescending(k => k.Length)
			.Select(Regex.Escape));
		return new Regex($@"(?<!\w)(?:{pattern})(?!\w)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
	}

	private static Dictionary<string, string> BuildReplacementMap(string? aliasName, IEnumerable<WordDictionaryEntry> entries)
	{
		var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		var active = entries
			.Where(e => e.Enabled && !string.IsNullOrEmpty(e.Word) && !string.IsNullOrWhiteSpace(e.Replacement))
			.ToList();

		foreach (var e in active.Where(e => e.IsGlobal))
			map[e.Word] = e.Replacement;

		if (!string.IsNullOrWhiteSpace(aliasName))
			foreach (var e in active.Where(e => !e.IsGlobal && string.Equals(e.AliasName.Trim(), aliasName.Trim(), StringComparison.OrdinalIgnoreCase)))
				map[e.Word] = e.Replacement;

		return map;
	}
}
