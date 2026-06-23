using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace OpenSpeaker.Text;

public class EmoteStripper
{
    private HashSet<string> _twitchEmotes = new();
    private HashSet<string> _bttvEmotes = new();
    private HashSet<string> _ffzEmotes = new();
    private HashSet<string> _sevenTvEmotes = new();
    private static readonly Regex CheermotePattern = new(@"\b[A-Za-z]+\d+\b", RegexOptions.Compiled);
    private static readonly Regex TwemojiPattern = new(@"[\uD800-\uDFFF]", RegexOptions.Compiled);

    public void SetTwitchEmotes(IEnumerable<string> emotes) => _twitchEmotes = new HashSet<string>(emotes);
    public void SetBttvEmotes(IEnumerable<string> emotes) => _bttvEmotes = new HashSet<string>(emotes);
    public void SetFfzEmotes(IEnumerable<string> emotes) => _ffzEmotes = new HashSet<string>(emotes);
    public void SetSevenTvEmotes(IEnumerable<string> emotes) => _sevenTvEmotes = new HashSet<string>(emotes);

    public IReadOnlyList<(string Word, string? EmoteSource)> DebugStrip(string message)
    {
        return message.Split(' ').Select(word =>
        {
            if (_twitchEmotes.Contains(word)) return (word, "Twitch");
            if (_bttvEmotes.Contains(word)) return (word, "BTTV");
            if (_ffzEmotes.Contains(word)) return (word, "FFZ");
            if (_sevenTvEmotes.Contains(word)) return (word, "7TV");
            if (CheermotePattern.IsMatch(word)) return (word, "Cheermote");
            return (word, (string?)null);
        }).ToList();
    }

    public string Strip(string message, bool stripTwitch, bool stripBttv, bool stripFfz, bool stripSevenTv, bool stripCheermotes, bool stripTwemoji, bool allowFirst, ICollection<string>? allowedEmotes = null, IReadOnlyList<string>? messageEmotes = null, IReadOnlyList<string>? messageCheermotes = null)
    {
        var messageEmoteSet = messageEmotes is { Count: > 0 } ? new HashSet<string>(messageEmotes) : null;
        var messageCheermoteSet = messageCheermotes is { Count: > 0 } ? new HashSet<string>(messageCheermotes) : null;
        var words = message.Split(' ');
        var result = new List<string>();
        bool foundFirst = false;

        foreach (var word in words)
        {
            bool isEmote = false;

            if (allowedEmotes != null && allowedEmotes.Contains(word)) { result.Add(word); continue; }

            if (stripTwitch && (_twitchEmotes.Contains(word) || messageEmoteSet?.Contains(word) == true)) isEmote = true;
            if (!isEmote && stripBttv && _bttvEmotes.Contains(word)) isEmote = true;
            if (!isEmote && stripFfz && _ffzEmotes.Contains(word)) isEmote = true;
            if (!isEmote && stripSevenTv && _sevenTvEmotes.Contains(word)) isEmote = true;
            if (!isEmote && stripCheermotes && messageCheermoteSet != null)
                isEmote = messageCheermoteSet.Contains(word);

            if (isEmote && allowFirst && !foundFirst)
            {
                foundFirst = true;
                result.Add(word);
                continue;
            }

            if (!isEmote)
                result.Add(word);
        }

        var stripped = string.Join(" ", result);

        if (stripTwemoji)
            stripped = TwemojiPattern.Replace(stripped, string.Empty);

        return stripped.Trim();
    }
}
