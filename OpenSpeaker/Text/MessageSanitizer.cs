using System.Linq;
using System.Text.RegularExpressions;
using OpenSpeaker.Data;
namespace OpenSpeaker.Text;

public class MessageSanitizer
{
    private static readonly Regex UrlRegex = new(@"https?://\S+|www\.\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly EmoteStripper _emoteStripper;
    private readonly PrefixChecker _prefixChecker;
    private readonly RegexReplacer _regexReplacer;
    private readonly SettingsRepository _settingsRepo;
    private readonly DatabaseContext _db;

    public MessageSanitizer(
        EmoteStripper emoteStripper,
        PrefixChecker prefixChecker,
        RegexReplacer regexReplacer,
        SettingsRepository settingsRepo,
        DatabaseContext db)
    {
        _emoteStripper = emoteStripper;
        _prefixChecker = prefixChecker;
        _regexReplacer = regexReplacer;
        _settingsRepo = settingsRepo;
        _db = db;
    }

    public bool IsIgnoredPrefix(string message)
    {
        var settings = _settingsRepo.GetSettings();
        return _prefixChecker.StartsWithIgnoredPrefix(message, settings.IgnoredPrefixes);
    }

    public string Sanitize(string message, bool applyFilters = true)
    {
        var settings = _settingsRepo.GetSettings();

        message = _emoteStripper.Strip(
            message,
            settings.StripTwitchEmotes,
            settings.StripBttvEmotes,
            settings.StripFfzEmotes,
            settings.StripSevenTvEmotes,
            settings.StripCheermotes,
            settings.StripTwemoji,
            settings.AllowFirstEmote,
            settings.AllowedEmotes.Count > 0 ? settings.AllowedEmotes : null);

        if (settings.UrlFilterMode == "Block" && UrlRegex.IsMatch(message))
            return string.Empty;
        if (settings.UrlFilterMode == "Strip")
            message = UrlRegex.Replace(message, string.Empty);

        if (applyFilters)
        {
            var filters = _db.RegexReplacements.FindAll();
            message = _regexReplacer.Replace(message, filters);
        }

        if (settings.MaxWords > 0)
            message = TruncateWords(message, settings.MaxWords, settings.WordLimitSymbolsAsSpaces);
        if (settings.MaxChars > 0 && message.Length > settings.MaxChars)
            message = message[..settings.MaxChars];

        return message.Trim();
    }

    private static string TruncateWords(string text, int maxWords, bool symbolsAsSpaces)
    {
        var words = symbolsAsSpaces
            ? Regex.Split(text, @"[^a-zA-Z0-9]+").Where(w => w.Length > 0)
            : text.Split(' ', StringSplitOptions.RemoveEmptyEntries).AsEnumerable();
        return string.Join(" ", words.Take(maxWords));
    }
}
