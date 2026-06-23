using System.Linq;
using System.Text.RegularExpressions;
using OpenSpeaker.Data;
using OpenSpeaker.Infrastructure.Logging;
namespace OpenSpeaker.Text;

public class MessageSanitizer
{
    private static readonly Regex UrlRegex = new(@"https?://\S+|www\.\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly EmoteStripper _emoteStripper;
    private readonly PrefixChecker _prefixChecker;
    private readonly RegexReplacer _regexReplacer;
    private readonly SettingsRepository _settingsRepo;
    private readonly RegexReplacementRepository _regexRepo;
    private readonly IAppLogger? _logger;

    public MessageSanitizer(
        EmoteStripper emoteStripper,
        PrefixChecker prefixChecker,
        RegexReplacer regexReplacer,
        SettingsRepository settingsRepo,
        RegexReplacementRepository regexRepo,
        IAppLogger? logger = null)
    {
        _emoteStripper = emoteStripper;
        _prefixChecker = prefixChecker;
        _regexReplacer = regexReplacer;
        _settingsRepo = settingsRepo;
        _regexRepo = regexRepo;
        _logger = logger;
    }

    public bool IsIgnoredPrefix(string message)
    {
        var settings = _settingsRepo.GetSettings();
        return _prefixChecker.StartsWithIgnoredPrefix(message, settings.IgnoredPrefixes);
    }

    public string Sanitize(string message, bool applyFilters = true, IReadOnlyList<string>? messageEmotes = null, IReadOnlyList<string>? messageCheermotes = null)
    {
        var settings = _settingsRepo.GetSettings();
        _logger?.Info($"SANITIZE :: Input='{message}'");

        var afterEmote = _emoteStripper.Strip(
            message,
            settings.StripTwitchEmotes,
            settings.StripBttvEmotes,
            settings.StripFfzEmotes,
            settings.StripSevenTvEmotes,
            settings.StripCheermotes,
            settings.StripTwemoji,
            settings.AllowFirstEmote,
            settings.AllowedEmotes.Count > 0 ? settings.AllowedEmotes : null,
            messageEmotes,
            messageCheermotes);
        if (afterEmote != message) _logger?.Info($"SANITIZE :: AfterEmoteStrip='{afterEmote}'");
        message = afterEmote;

        if (settings.UrlFilterMode == "Block" && UrlRegex.IsMatch(message))
        {
            _logger?.Info("SANITIZE :: Dropped - URL block");
            return string.Empty;
        }
        if (settings.UrlFilterMode == "Strip")
        {
            var afterUrl = UrlRegex.Replace(message, string.Empty);
            if (afterUrl != message) _logger?.Info($"SANITIZE :: AfterUrlStrip='{afterUrl}'");
            message = afterUrl;
        }

        if (applyFilters)
        {
            var filters = _regexRepo.GetAll();
            foreach (var f in filters.Where(x => x.Enabled).OrderBy(x => x.Order))
            {
                var before = message;
                message = _regexReplacer.ApplySingle(message, f);
                if (message != before)
                    _logger?.Info($"SANITIZE :: Replacement '{f.Pattern}' → '{f.Replacement}' changed message to '{message}'");
                if (string.IsNullOrEmpty(message))
                {
                    _logger?.Info($"SANITIZE :: Dropped - replacement '{f.Pattern}' emptied message");
                    return string.Empty;
                }
            }
        }

        if (settings.MaxWords > 0)
        {
            var afterWords = TruncateWords(message, settings.MaxWords, settings.WordLimitSymbolsAsSpaces);
            if (afterWords != message) _logger?.Info($"SANITIZE :: AfterWordLimit({settings.MaxWords})='{afterWords}'");
            message = afterWords;
        }
        if (settings.MaxChars > 0 && message.Length > settings.MaxChars)
        {
            _logger?.Info($"SANITIZE :: AfterCharLimit({settings.MaxChars})");
            message = message[..settings.MaxChars];
        }

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
