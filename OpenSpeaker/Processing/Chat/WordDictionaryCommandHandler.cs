using OpenSpeaker.Data;
using OpenSpeaker.Models;
using OpenSpeaker.Twitch;
namespace OpenSpeaker.Chat;

public class WordDictionaryCommandHandler : IChatCommandHandler
{
	private const string Prefix = "!ttsdict";

	private readonly WordDictionaryRepository _wordDictionary;
	private readonly SettingsRepository _settingsRepo;
	private readonly ITwitchService _twitch;

	public WordDictionaryCommandHandler(WordDictionaryRepository wordDictionary, SettingsRepository settingsRepo, ITwitchService twitch)
	{
		_wordDictionary = wordDictionary;
		_settingsRepo = settingsRepo;
		_twitch = twitch;
	}

	public async Task<bool> HandleAsync(string twitchId, string username, List<string> roles, string rawMessage)
	{
		if (!rawMessage.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
			return false;

		var rest = rawMessage.Substring(Prefix.Length).Trim();
		if (rest.Length == 0)
			return true;

		if (!IsAllowed(roles))
			return true;

		var parts = rest.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length < 2)
			return true;

		var scope = parts[0];
		var action = parts[1];
		var argsText = parts.Length > 2 ? parts[2].Trim() : string.Empty;
		var aliasName = scope.Equals("global", StringComparison.OrdinalIgnoreCase) ? string.Empty : scope;

		if (action.Equals("add", StringComparison.OrdinalIgnoreCase))
			await HandleAdd(aliasName, argsText);
		else if (action.Equals("remove", StringComparison.OrdinalIgnoreCase))
			await HandleRemove(aliasName, argsText);

		return true;
	}

	private static bool IsAllowed(List<string> roles) =>
		roles.Contains(UserRoles.Broadcaster) || roles.Contains(UserRoles.Moderator);

	private async Task HandleAdd(string aliasName, string argsText)
	{
		var parts = argsText.Split("->", 2, StringSplitOptions.None);
		if (parts.Length < 2) return;
		var word = parts[0].Trim();
		var replacement = parts[1].Trim();
		if (word.Length == 0 || replacement.Length == 0) return;

		var existing = _wordDictionary.GetAll().FirstOrDefault(e =>
			e.AliasName.Equals(aliasName, StringComparison.OrdinalIgnoreCase) &&
			e.Word.Equals(word, StringComparison.OrdinalIgnoreCase));

		if (existing != null)
		{
			existing.Replacement = replacement;
			_wordDictionary.Upsert(existing);
		}
		else
		{
			_wordDictionary.Insert(new WordDictionaryEntry { Word = word, Replacement = replacement, AliasName = aliasName });
		}

		await Reply($"Added \"{word}\" -> \"{replacement}\" ({(string.IsNullOrEmpty(aliasName) ? "global" : aliasName)}).");
	}

	private async Task HandleRemove(string aliasName, string word)
	{
		word = word.Trim();
		if (word.Length == 0) return;

		var existing = _wordDictionary.GetAll().FirstOrDefault(e =>
			e.AliasName.Equals(aliasName, StringComparison.OrdinalIgnoreCase) &&
			e.Word.Equals(word, StringComparison.OrdinalIgnoreCase));

		if (existing == null)
		{
			await Reply($"No entry found for \"{word}\" ({(string.IsNullOrEmpty(aliasName) ? "global" : aliasName)}).");
			return;
		}

		_wordDictionary.Delete(existing.Id);
		await Reply($"Removed \"{word}\" ({(string.IsNullOrEmpty(aliasName) ? "global" : aliasName)}).");
	}

	private async Task Reply(string text)
	{
		if (_settingsRepo.GetSettings().SilenceCommandOutput) return;
		await _twitch.SendChatMessageAsync(text);
	}
}
